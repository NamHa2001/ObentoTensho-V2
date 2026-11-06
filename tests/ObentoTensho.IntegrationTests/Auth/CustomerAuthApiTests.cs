using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Infrastructure.Messaging;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Auth;

// docs/srs/10 §11: tiêu chí nghiệm thu đăng ký, đăng nhập, làm mới phiên, đăng xuất của khách
[Collection(SqlServerFixture.CollectionName)]
public sealed class CustomerAuthApiTests : IDisposable
{
    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly AuthApiClient _api;

    public CustomerAuthApiTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _api = new AuthApiClient(_factory.CreateApiClient());
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Dang_ky_email_moi_va_email_da_co_phan_hoi_giong_het_va_khong_cap_phien()
    {
        var email = AuthApiClient.NewEmail();

        var first = await _api.RegisterAsync(email);
        var second = await _api.RegisterAsync(email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Null(AuthApiClient.GetRefreshSetCookie(first));
        Assert.Null(AuthApiClient.GetRefreshSetCookie(second));

        await using var db = _fixture.CreateContext();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.Equal("+84912345678", user.PhoneNumber);
        Assert.False(user.IsEmailVerified);
        Assert.NotNull(user.EmailVerifyTokenHash);

        var mails = await db.OutboxMessages.Where(m => m.PayloadJson.Contains(email)).Select(m => m.Type).ToListAsync();
        Assert.Contains(AccountEmailOutbox.EmailVerificationType, mails, StringComparer.Ordinal);
        Assert.Contains(AccountEmailOutbox.DuplicateRegistrationType, mails, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Dang_ky_du_lieu_sai_tra_400_kem_ma_loi_tung_truong()
    {
        var response = await _api.RegisterAsync("khong-phai-email", password: "ngan");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ErrorCodes.ValidationFailed, body.GetProperty("errorCode").GetString());
        var errors = body.GetProperty("errors");
        Assert.Equal(FieldErrorCodes.InvalidFormat, errors.GetProperty("email")[0].GetString());
        Assert.Equal(FieldErrorCodes.TooShort, errors.GetProperty("password")[0].GetString());
    }

    [Fact]
    public async Task Dang_ky_mat_khau_pho_bien_bi_tu_choi()
    {
        var response = await _api.RegisterAsync(AuthApiClient.NewEmail(), password: "Password123");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(FieldErrorCodes.PasswordTooCommon, body.GetProperty("errors").GetProperty("password")[0].GetString());
    }

    [Fact]
    public async Task Body_co_truong_la_bi_tu_choi()
    {
        var http = _factory.CreateApiClient();

        var response = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "a@b.vn", password = "x", isAdmin = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, await AuthApiClient.ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Dang_nhap_dung_tra_access_token_va_cookie_refresh_an_toan()
    {
        var email = AuthApiClient.NewEmail();
        (await _api.RegisterAsync(email)).EnsureSuccessStatusCode();

        var response = await _api.LoginAsync(email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(await AuthApiClient.ReadAccessTokenAsync(response));
        var cookie = AuthApiClient.GetRefreshSetCookie(response)?.ToLowerInvariant();
        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain("domain=", cookie, StringComparison.Ordinal);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sai_mat_khau_va_email_khong_ton_tai_cung_401_INVALID_CREDENTIALS()
    {
        var email = AuthApiClient.NewEmail();
        (await _api.RegisterAsync(email)).EnsureSuccessStatusCode();

        var wrongPassword = await _api.LoginAsync(email, "Sai-mat-khau-2026");
        var unknownEmail = await _api.LoginAsync(AuthApiClient.NewEmail());

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, await AuthApiClient.ReadErrorCodeAsync(wrongPassword));
        Assert.Equal(ErrorCodes.InvalidCredentials, await AuthApiClient.ReadErrorCodeAsync(unknownEmail));
    }

    [Fact]
    public async Task Sai_5_lan_thi_lan_6_dung_mat_khau_van_bi_tu_choi()
    {
        var email = AuthApiClient.NewEmail();
        (await _api.RegisterAsync(email)).EnsureSuccessStatusCode();

        for (var i = 0; i < 5; i++)
        {
            await _api.LoginAsync(email, "Sai-mat-khau-2026");
        }

        var response = await _api.LoginAsync(email);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var db = _fixture.CreateContext();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.NotNull(user.LockoutEndUtc);
    }

    [Fact]
    public async Task Lam_moi_xoay_token_va_dung_lai_token_cu_thi_thu_hoi_ca_gia_dinh()
    {
        var (_, first) = await _api.RegisterAndLoginAsync(AuthApiClient.NewEmail());

        var rotated = await _api.RefreshAsync(first);
        var second = AuthApiClient.ReadRefreshCookie(rotated);
        var reused = await _api.RefreshAsync(first);
        var afterReuse = await _api.RefreshAsync(second);

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        Assert.NotNull(second);
        Assert.NotEqual(first, second, StringComparer.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Lam_moi_thieu_header_rieng_hoac_sai_origin_bi_403()
    {
        var (_, refresh) = await _api.RegisterAndLoginAsync(AuthApiClient.NewEmail());

        var response = await _api.RefreshAsync(refresh, trusted: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.Forbidden, await AuthApiClient.ReadErrorCodeAsync(response));
        Assert.Equal(HttpStatusCode.OK, (await _api.RefreshAsync(refresh)).StatusCode);
    }

    [Fact]
    public async Task Tai_khoan_bi_khoa_thi_refresh_tra_423_va_mat_phien()
    {
        var email = AuthApiClient.NewEmail();
        var (_, refresh) = await _api.RegisterAndLoginAsync(email);
        await using (var db = _fixture.CreateContext())
        {
            await db.Users.Where(u => u.Email == email).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        }

        var response = await _api.RefreshAsync(refresh);

        Assert.Equal(HttpStatusCode.Locked, response.StatusCode);
        Assert.Equal(ErrorCodes.AccountLocked, await AuthApiClient.ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Dang_xuat_thu_hoi_token_va_xoa_cookie()
    {
        var (_, refresh) = await _api.RegisterAndLoginAsync(AuthApiClient.NewEmail());

        var logout = await _api.LogoutAsync(refresh);
        var refreshAfter = await _api.RefreshAsync(refresh);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains("expires=thu, 01 jan 1970", AuthApiClient.GetRefreshSetCookie(logout)?.ToLowerInvariant(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfter.StatusCode);
    }

    [Fact]
    public async Task Access_token_hop_le_duoc_vao_va_bi_tu_choi_ngay_khi_doi_security_stamp()
    {
        var validEmail = AuthApiClient.NewEmail();
        var revokedEmail = AuthApiClient.NewEmail();
        var (validToken, _) = await _api.RegisterAndLoginAsync(validEmail);
        var (revokedToken, _) = await _api.RegisterAndLoginAsync(revokedEmail);
        await using (var db = _fixture.CreateContext())
        {
            await db.Users.Where(u => u.Email == revokedEmail)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, new string('0', 32)));
        }

        var ok = await _api.GetProtectedAsync(validToken);
        var revoked = await _api.GetProtectedAsync(revokedToken);
        var tampered = await _api.GetProtectedAsync(validToken[..^2] + "xx");

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, tampered.StatusCode);
        Assert.Equal(ErrorCodes.Unauthenticated, await AuthApiClient.ReadErrorCodeAsync(revoked));
    }

    [Fact]
    public async Task Dang_nhap_qua_5_phien_thi_phien_cu_nhat_bi_thu_hoi()
    {
        var email = AuthApiClient.NewEmail();
        var (_, oldest) = await _api.RegisterAndLoginAsync(email);
        for (var i = 0; i < 5; i++)
        {
            (await _api.LoginAsync(email)).EnsureSuccessStatusCode();
        }

        await using var db = _fixture.CreateContext();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        var active = await db.RefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAtUtc == null);
        var limited = await db.RefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedReason == RefreshTokenRevokedReason.SessionLimit);

        Assert.Equal(5, active);
        Assert.Equal(1, limited);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.RefreshAsync(oldest)).StatusCode);
    }
}
