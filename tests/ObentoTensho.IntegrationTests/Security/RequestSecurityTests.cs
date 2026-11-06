using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Security;

// SEC-02 (IP thật), docs/srs/03 §2 (giới hạn tần suất), SEC-09/68 (header), SEC-12 (quyền), BR-STF-10/11 (audit)
[Collection(SqlServerFixture.CollectionName)]
public sealed class RequestSecurityTests(SqlServerFixture fixture)
{
    private const string ProxyNetwork = "10.0.0.0/8";

    private ApiFactory LimitedFactory() => new(fixture.ConnectionString, new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["RateLimiting:LoginPerMinute"] = "3",
        ["Network:KnownNetworks:0"] = ProxyNetwork,
    });

    private static HttpRequestMessage Login(string remoteIp, string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "khong.co@example.com", password = "Sai-mat-khau-123" }),
        };
        request.Headers.Add(TestRemoteIpStartupFilter.Header, remoteIp);
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return request;
    }

    [Fact]
    public async Task Vuot_gioi_han_dang_nhap_tra_429_RATE_LIMITED_kem_Retry_After()
    {
        using var factory = LimitedFactory();
        using var http = factory.CreateApiClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(Login("203.0.113.10"))).StatusCode);
        }

        var limited = await http.SendAsync(Login("203.0.113.10"));

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(ErrorCodes.RateLimited, await AuthApiClient.ReadErrorCodeAsync(limited));
        Assert.NotNull(limited.Headers.RetryAfter);
    }

    [Fact]
    public async Task Hai_khach_sau_cung_proxy_co_hai_bo_dem_rieng_va_IP_gia_tu_client_bi_bo_qua()
    {
        using var factory = LimitedFactory();
        using var http = factory.CreateApiClient();

        for (var i = 0; i < 3; i++)
        {
            await http.SendAsync(Login("10.0.0.5", forwardedFor: "198.51.100.1"));
        }

        // Cùng proxy nhưng khách khác: chưa bị chặn
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(Login("10.0.0.5", forwardedFor: "198.51.100.2"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await http.SendAsync(Login("10.0.0.5", forwardedFor: "198.51.100.1"))).StatusCode);

        // Client không qua proxy tự đặt X-Forwarded-For: không được tin, vẫn tính theo IP kết nối
        for (var i = 0; i < 3; i++)
        {
            await http.SendAsync(Login("203.0.113.20", forwardedFor: $"192.0.2.{i}"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await http.SendAsync(Login("203.0.113.20", forwardedFor: "192.0.2.99"))).StatusCode);
    }

    [Fact]
    public async Task Phan_hoi_co_header_bao_mat_va_khong_co_header_Server()
    {
        using var factory = new ApiFactory(fixture.ConnectionString);
        using var http = factory.CreateApiClient();

        var response = await http.GetAsync("/health/live");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task Quyen_theo_ma_tran_bien_tap_vien_vao_noi_dung_bep_thi_403()
    {
        using var factory = new ApiFactory(fixture.ConnectionString);
        var admin = new AdminAuthApiClient(factory, fixture);
        var editorToken = await LoginStaffAsync(admin, StaffRole.ContentEditor);
        var kitchenToken = await LoginStaffAsync(admin, StaffRole.Kitchen);

        var allowed = await admin.GetAsync("/api/v1/admin/test-probe/content", editorToken);
        var denied = await admin.GetAsync("/api/v1/admin/test-probe/content", kitchenToken);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(ErrorCodes.Forbidden, await AuthApiClient.ReadErrorCodeAsync(denied));
        Assert.Contains("no-store", allowed.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dang_nhap_nhan_vien_ghi_audit_che_email_co_IP_va_CorrelationId()
    {
        using var factory = new ApiFactory(fixture.ConnectionString);
        var admin = new AdminAuthApiClient(factory, fixture);
        var (id, email) = await admin.CreateStaffAsync(StaffRole.Reception);

        await admin.LoginAsync(email, "Sai-mat-khau-2026");
        await admin.LoginAsync(email);

        await using var db = fixture.CreateContext();
        var logs = await db.AuditLogs.AsNoTracking().Where(a => a.EntityId == id.ToString()).OrderBy(a => a.Id).ToListAsync();
        Assert.Equal([AuditActions.StaffLoginFailed, AuditActions.StaffLoginSucceeded], logs.Select(l => l.Action), StringComparer.Ordinal);

        var failed = JsonDocument.Parse(logs[0].NewDataJson ?? "{}").RootElement;
        Assert.Equal("wrong_password", failed.GetProperty("reason").GetString());
        Assert.StartsWith("n***@", failed.GetProperty("email").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(email, logs[0].NewDataJson, StringComparison.Ordinal);
        Assert.Equal(AuditActorType.Staff, logs[1].ActorType);
        Assert.Equal(id, logs[1].ActorId);
        Assert.All(logs, l => Assert.NotEqual(Guid.Empty, l.CorrelationId));
        Assert.All(logs, l => Assert.False(string.IsNullOrEmpty(l.IpAddress)));
    }

    private static async Task<string> LoginStaffAsync(AdminAuthApiClient admin, StaffRole role)
    {
        var (_, email) = await admin.CreateStaffAsync(role);
        var data = await AdminAuthApiClient.ReadDataAsync(await admin.LoginAsync(email));
        return data.GetProperty("session").GetProperty("accessToken").GetString() ?? string.Empty;
    }
}
