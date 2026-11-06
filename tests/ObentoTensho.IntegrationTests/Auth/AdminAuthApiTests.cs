using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Auth;

// docs/srs/10 §4.2, §11 và SEC-15/44/74: đăng nhập nhân viên, 2FA, mời, đổi mật khẩu bắt buộc
[Collection(SqlServerFixture.CollectionName)]
public sealed class AdminAuthApiTests : IDisposable
{
    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly AdminAuthApiClient _api;

    public AdminAuthApiTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _api = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Le_tan_khong_bat_buoc_2FA_dang_nhap_vao_thang_va_nhan_cookie_admin()
    {
        var (_, email) = await _api.CreateStaffAsync(StaffRole.Reception);

        var response = await _api.LoginAsync(email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await AdminAuthApiClient.ReadDataAsync(response);
        Assert.False(string.IsNullOrEmpty(data.GetProperty("session").GetProperty("accessToken").GetString()));
        Assert.NotNull(AdminAuthApiClient.ReadAdminCookie(response));
    }

    [Fact]
    public async Task Token_nhan_vien_va_token_khach_khong_dung_lan_duoc()
    {
        var (_, email) = await _api.CreateStaffAsync(StaffRole.Kitchen);
        var staffToken = (await AdminAuthApiClient.ReadDataAsync(await _api.LoginAsync(email)))
            .GetProperty("session").GetProperty("accessToken").GetString() ?? string.Empty;
        var customer = new AuthApiClient(_factory.CreateApiClient());
        var (customerToken, _) = await customer.RegisterAndLoginAsync(AuthApiClient.NewEmail());

        Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync("/api/v1/admin/test-probe", staffToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.GetAsync("/api/v1/admin/test-probe", customerToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.GetAsync("/api/v1/test-probe", staffToken)).StatusCode);
    }

    [Fact]
    public async Task Quan_ly_chua_bat_2FA_phai_thiet_lap_roi_moi_vao_duoc()
    {
        var (id, email) = await _api.CreateStaffAsync(StaffRole.Manager);

        var challenge = await AdminAuthApiClient.ReadChallengeAsync(await _api.LoginAsync(email));
        Assert.Equal("totp_setup", challenge?.Kind);

        var setup = await _api.PostAsync("2fa/setup", new { challengeToken = challenge!.Value.Token });
        var setupData = await AdminAuthApiClient.ReadDataAsync(setup);
        Assert.StartsWith("otpauth://totp/", setupData.GetProperty("otpAuthUri").GetString(), StringComparison.Ordinal);

        var secret = await _api.ReadTotpSecretAsync(id);
        var confirm = await _api.PostAsync("2fa/setup/confirm", new { challengeToken = challenge.Value.Token, code = AdminAuthApiClient.CurrentCode(secret) });

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var data = await AdminAuthApiClient.ReadDataAsync(confirm);
        Assert.Equal(10, data.GetProperty("recoveryCodes").GetArrayLength());
        Assert.NotNull(AdminAuthApiClient.ReadAdminCookie(confirm));
    }

    [Fact]
    public async Task Ma_TOTP_da_dung_bi_tu_choi_ma_buoc_moi_va_ma_khoi_phuc_dung_mot_lan()
    {
        var (id, email, recoveryCodes, usedStep) = await CreateManagerWithTwoFactorAsync();
        var secret = await _api.ReadTotpSecretAsync(id);

        // Mã của bước vừa dùng lúc thiết lập bị từ chối; mã của bước kế tiếp (vẫn trong độ lệch cho phép) được nhận
        var token = (await AdminAuthApiClient.ReadChallengeAsync(await _api.LoginAsync(email)))!.Value.Token;
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.PostAsync("2fa/verify", new { challengeToken = token, code = Totp.ComputeCode(secret, usedStep) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.PostAsync("2fa/verify", new { challengeToken = token, code = Totp.ComputeCode(secret, usedStep + 1) })).StatusCode);

        var recovery = recoveryCodes[0].ToLowerInvariant();
        var token2 = (await AdminAuthApiClient.ReadChallengeAsync(await _api.LoginAsync(email)))!.Value.Token;
        Assert.Equal(HttpStatusCode.OK, (await _api.PostAsync("2fa/verify", new { challengeToken = token2, recoveryCode = recovery })).StatusCode);
        var token3 = (await AdminAuthApiClient.ReadChallengeAsync(await _api.LoginAsync(email)))!.Value.Token;
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.PostAsync("2fa/verify", new { challengeToken = token3, recoveryCode = recovery })).StatusCode);
    }

    [Fact]
    public async Task Sai_TOTP_5_lan_thi_tai_khoan_bi_khoa()
    {
        var (_, email, _, _) = await CreateManagerWithTwoFactorAsync();

        for (var i = 0; i < 5; i++)
        {
            var token = (await AdminAuthApiClient.ReadChallengeAsync(await _api.LoginAsync(email)))?.Token;
            var wrong = await _api.PostAsync("2fa/verify", new { challengeToken = token, code = "000000" });
            Assert.Equal(ErrorCodes.InvalidCredentials, await AuthApiClient.ReadErrorCodeAsync(wrong));
        }

        var locked = await _api.LoginAsync(email);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, await AuthApiClient.ReadErrorCodeAsync(locked));
    }

    [Fact]
    public async Task SuperAdmin_moi_phai_doi_mat_khau_roi_thiet_lap_2FA_ma_tam_cu_mat_hieu_luc()
    {
        var (_, email) = await _api.CreateStaffAsync(StaffRole.SuperAdmin, mustChangePassword: true);

        var first = await AdminAuthApiClient.ReadChallengeAsync(await _api.LoginAsync(email));
        Assert.Equal("password_change", first?.Kind);

        const string newPassword = "Mat-khau-moi-cua-chu-quan";
        var changed = await _api.PostAsync("change-password", new { challengeToken = first!.Value.Token, currentPassword = AdminAuthApiClient.Password, newPassword });
        Assert.Equal("totp_setup", (await AdminAuthApiClient.ReadChallengeAsync(changed))?.Kind);

        var replay = await _api.PostAsync("change-password", new { challengeToken = first.Value.Token, currentPassword = newPassword, newPassword = "Lai-doi-lan-nua-2026" });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.LoginAsync(email)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.LoginAsync(email, newPassword)).StatusCode);
    }

    [Fact]
    public async Task Ma_tam_sai_loai_bi_tu_choi()
    {
        var (_, email) = await _api.CreateStaffAsync(StaffRole.Manager);
        var setupToken = (await AdminAuthApiClient.ReadChallengeAsync(await _api.LoginAsync(email)))!.Value.Token;

        var response = await _api.PostAsync("2fa/verify", new { challengeToken = setupToken, code = "123456" });
        var tampered = await _api.PostAsync("2fa/setup", new { challengeToken = setupToken[..^4] + "AAAA" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, tampered.StatusCode);
    }

    [Fact]
    public async Task Nhan_loi_moi_dat_mat_khau_mot_lan_roi_dang_nhap_duoc()
    {
        var tokens = _factory.Services.GetRequiredService<ISecureTokenService>();
        var invite = tokens.Create();
        var (id, email) = await _api.CreateStaffAsync(StaffRole.Cashier, passwordHash: string.Empty);
        await using (var db = _fixture.CreateContext())
        {
            await db.StaffMembers.Where(s => s.Id == id).ExecuteUpdateAsync(u => u
                .SetProperty(s => s.PasswordHash, (string?)null)
                .SetProperty(s => s.InviteTokenHash, invite.Hash)
                .SetProperty(s => s.InviteExpiryUtc, DateTime.UtcNow.AddHours(72)));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.LoginAsync(email)).StatusCode);

        var accept = await _api.PostAsync("accept-invite", new { token = invite.Value, password = "Thu-ngan-ca-chieu" });
        var again = await _api.PostAsync("accept-invite", new { token = invite.Value, password = "Thu-ngan-ca-toi-99" });

        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await AuthApiClient.ReadErrorCodeAsync(again));
        Assert.Equal(HttpStatusCode.OK, (await _api.LoginAsync(email, "Thu-ngan-ca-chieu")).StatusCode);
    }

    [Fact]
    public async Task Lam_moi_phien_admin_xoay_token_va_tu_choi_origin_trang_khach()
    {
        var (_, email) = await _api.CreateStaffAsync(StaffRole.ContentEditor);
        var refresh = AdminAuthApiClient.ReadAdminCookie(await _api.LoginAsync(email));

        var fromCustomerSite = await _api.RefreshAsync(refresh, ApiFactory.CustomerOrigin);
        var rotated = await _api.RefreshAsync(refresh);
        var reused = await _api.RefreshAsync(refresh);

        Assert.Equal(HttpStatusCode.Forbidden, fromCustomerSite.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        Assert.NotNull(AdminAuthApiClient.ReadAdminCookie(rotated));
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task Phien_admin_het_sau_30_phut_khong_hoat_dong()
    {
        var (id, email) = await _api.CreateStaffAsync(StaffRole.Reception);
        var refresh = AdminAuthApiClient.ReadAdminCookie(await _api.LoginAsync(email));

        await using var db = _fixture.CreateContext();
        var token = await db.RefreshTokens.SingleAsync(t => t.StaffId == id);
        Assert.InRange(token.ExpiresAtUtc - token.CreatedAtUtc, TimeSpan.FromMinutes(29), TimeSpan.FromMinutes(31));
        Assert.NotNull(refresh);
    }

    private async Task<(Guid Id, string Email, IReadOnlyList<string> RecoveryCodes, long UsedStep)> CreateManagerWithTwoFactorAsync()
    {
        var (id, email) = await _api.CreateStaffAsync(StaffRole.Manager);
        var token = (await AdminAuthApiClient.ReadChallengeAsync(await _api.LoginAsync(email)))!.Value.Token;
        (await _api.PostAsync("2fa/setup", new { challengeToken = token })).EnsureSuccessStatusCode();
        var secret = await _api.ReadTotpSecretAsync(id);
        var step = Totp.GetStep(DateTime.UtcNow);
        var confirm = await _api.PostAsync("2fa/setup/confirm", new { challengeToken = token, code = Totp.ComputeCode(secret, step) });
        var codes = (await AdminAuthApiClient.ReadDataAsync(confirm)).GetProperty("recoveryCodes")
            .EnumerateArray().Select(c => c.GetString() ?? string.Empty).ToArray();
        return (id, email, codes, step);
    }
}
