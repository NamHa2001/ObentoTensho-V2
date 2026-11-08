using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Auth;

// SEC-50: đăng xuất/thu hồi thiết bị ⇒ access token còn hạn hết dùng được ngay; SEC-58: báo đăng nhập quản trị từ nơi lạ
[Collection(SqlServerFixture.CollectionName)]
public sealed class SessionSecurityTests : IDisposable
{
    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AuthApiClient _api;
    private readonly AdminAuthApiClient _admin;

    public SessionSecurityTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _api = new AuthApiClient(_http);
        _admin = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Dang_xuat_thi_access_token_cua_phien_do_het_dung_ngay()
    {
        var email = AuthApiClient.NewEmail();
        var (access, refresh) = await _api.RegisterAndLoginAsync(email);
        var (otherAccess, _) = await LoginAgainAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await _api.GetProtectedAsync(access)).StatusCode);

        Assert.True((await _api.LogoutAsync(refresh)).IsSuccessStatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.GetProtectedAsync(access)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.GetProtectedAsync(otherAccess)).StatusCode);
    }

    [Fact]
    public async Task Thu_hoi_mot_thiet_bi_va_thu_hoi_tat_ca()
    {
        var email = AuthApiClient.NewEmail();
        var (first, _) = await _api.RegisterAndLoginAsync(email);
        var (second, _) = await LoginAgainAsync(email);
        var (third, _) = await LoginAgainAsync(email);

        var sessions = await AdminAuthApiClient.ReadDataAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/account/sessions", third));
        Assert.Equal(3, sessions.GetArrayLength());

        // Thu hồi đúng phiên của thiết bị thứ hai (mã phiên nằm trong claim sid)
        var target = Guid.Parse(new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(second).GetClaim("sid").Value);
        Assert.Contains(target, sessions.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
        Assert.Equal(HttpStatusCode.NoContent, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/account/sessions/{target}", third)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.GetProtectedAsync(second)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.GetProtectedAsync(first)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await _api.SendAsync(HttpMethod.Post, "/api/v1/account/sessions/revoke-all", third)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.GetProtectedAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.GetProtectedAsync(third)).StatusCode);

        // Đăng nhập lại sau đó vẫn bình thường
        var (fresh, _) = await LoginAgainAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await _api.GetProtectedAsync(fresh)).StatusCode);
    }

    [Fact]
    public async Task Dang_nhap_quan_tri_tu_thiet_bi_la_thi_bao_nhan_vien()
    {
        var (id, email) = await _admin.CreateStaffAsync(StaffRole.Kitchen);
        var key = $"staff_new_device:{id:N}:";

        Assert.Equal(HttpStatusCode.OK, (await StaffLoginAsync(email, "203.0.113.7", "Firefox/1")).StatusCode);
        Assert.Equal(0, await AlertsAsync(key));

        // Cùng trình duyệt, IP khác nhưng cùng dải /24 ⇒ quen
        Assert.Equal(HttpStatusCode.OK, (await StaffLoginAsync(email, "203.0.113.99", "Firefox/1")).StatusCode);
        Assert.Equal(0, await AlertsAsync(key));

        Assert.Equal(HttpStatusCode.OK, (await StaffLoginAsync(email, "198.51.100.5", "Firefox/1")).StatusCode);
        Assert.Equal(1, await AlertsAsync(key));
        Assert.Equal(HttpStatusCode.OK, (await StaffLoginAsync(email, "203.0.113.7", "Chrome/2")).StatusCode);
        Assert.Equal(2, await AlertsAsync(key));
    }

    [Fact]
    public async Task Bam_khong_phai_toi_thi_thu_hoi_phien_khoa_tai_khoan_va_gui_lien_ket_dat_lai()
    {
        var (id, email) = await _admin.CreateStaffAsync(StaffRole.Kitchen);
        Assert.Equal(HttpStatusCode.OK, (await StaffLoginAsync(email, "203.0.113.7", "Firefox/1")).StatusCode);
        var intruder = await StaffLoginAsync(email, "198.51.100.5", "Chrome/9");
        var session = (await AdminAuthApiClient.ReadDataAsync(intruder)).GetProperty("session").GetProperty("accessToken").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await _admin.GetAsync("/api/v1/admin/test-probe", session)).StatusCode);

        await _factory.ProcessOutboxAsync();
        var alert = Assert.Single(_factory.Emails.To(email), m => m.TextBody.Contains("/auth/not-me#token=", StringComparison.Ordinal));
        Assert.Contains("198.51.100.5", alert.TextBody, StringComparison.Ordinal);
        Assert.Contains("Chrome/9", alert.TextBody, StringComparison.Ordinal);
        var token = Orders.OrderApiClient.TokenIn(alert.TextBody);

        Assert.Equal(ErrorCodes.TokenInvalid, await AuthApiClient.ReadErrorCodeAsync(await _admin.PostAsync("not-me", new { token = token + "x" })));

        // Mã đúng chữ ký nhưng khác loại hoặc đã hết hạn đều bị từ chối
        await using (var db = _fixture.CreateContext())
        {
            var stamp = await db.StaffMembers.Where(x => x.Id == id).Select(x => x.SecurityStamp).SingleAsync();
            var tokens = _factory.Services.GetRequiredService<IStaffChallengeTokens>();
            var now = DateTime.UtcNow;
            foreach (var forged in new[]
            {
                tokens.Protect(new StaffChallenge(id, StaffChallengeKind.StepUp, stamp, now.AddMinutes(5), SecondFactorPassed: true)),
                tokens.Protect(new StaffChallenge(id, StaffChallengeKind.NotMe, stamp, now.AddSeconds(-1), SecondFactorPassed: false)),
            })
            {
                Assert.Equal(ErrorCodes.TokenInvalid, await AuthApiClient.ReadErrorCodeAsync(await _admin.PostAsync("not-me", new { token = forged })));
            }
        }

        Assert.Equal(HttpStatusCode.NoContent, (await _admin.PostAsync("not-me", new { token })).StatusCode);

        // Phiên của kẻ xấu hết dùng được ngay, mật khẩu cũ không đăng nhập được, liên kết chỉ dùng một lần
        Assert.Equal(HttpStatusCode.Unauthorized, (await _admin.GetAsync("/api/v1/admin/test-probe", session)).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await StaffLoginAsync(email, "198.51.100.5", "Chrome/9")).StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await AuthApiClient.ReadErrorCodeAsync(await _admin.PostAsync("not-me", new { token })));
        await using (var db = _fixture.CreateContext())
        {
            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "auth.staff.not_me_reported" && a.EntityId == id.ToString()));
            Assert.False(await db.RefreshTokens.AnyAsync(t => t.StaffId == id && t.RevokedAtUtc == null));
        }

        // Đặt lại mật khẩu qua liên kết trong thư thì mở khóa
        await _factory.ProcessOutboxAsync();
        var reset = Orders.OrderApiClient.TokenIn(Assert.Single(_factory.Emails.To(email), m => m.TextBody.Contains("/auth/reset-password#token=", StringComparison.Ordinal)).TextBody);
        const string NewPassword = "Bep-moi-an-toan-2026";
        Assert.Equal(HttpStatusCode.NoContent, (await _admin.PostAsync("reset-password", new { token = reset, newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await StaffLoginAsync(email, "198.51.100.5", "Chrome/9", NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Nhan_vien_sai_5_lan_tu_mot_IP_chi_chan_IP_do()
    {
        var (_, email) = await _admin.CreateStaffAsync(StaffRole.Kitchen);
        for (var i = 0; i < 5; i++)
        {
            await StaffLoginAsync(email, "203.0.113.50", "Firefox/1", "Sai-mat-khau-2026");
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await StaffLoginAsync(email, "203.0.113.50", "Firefox/1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await StaffLoginAsync(email, "198.51.100.50", "Firefox/1")).StatusCode);
    }

    private async Task<(string Access, string Refresh)> LoginAgainAsync(string email)
    {
        var login = await _api.LoginAsync(email);
        return (await AuthApiClient.ReadAccessTokenAsync(login), AuthApiClient.ReadRefreshCookie(login)!);
    }

    private async Task<HttpResponseMessage> StaffLoginAsync(string email, string ip, string userAgent, string password = AdminAuthApiClient.Password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/auth/login")
        {
            Content = JsonContent.Create(new { email, password }),
        };
        request.Headers.Add(TestRemoteIpStartupFilter.Header, ip);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await _http.SendAsync(request);
        await response.Content.LoadIntoBufferAsync();
        return response;
    }

    private async Task<int> AlertsAsync(string prefix)
    {
        await using var db = _fixture.CreateContext();
        return await db.OutboxMessages.CountAsync(m => m.IdempotencyKey.StartsWith(prefix));
    }
}
