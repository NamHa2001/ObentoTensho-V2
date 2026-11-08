using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Services;
using ObentoTensho.Infrastructure.Security;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Auth;

// SEC-51: từ lần đăng nhập sai thứ 3 của cặp (tài khoản, IP) thì phải kèm Turnstile
[Collection(SqlServerFixture.CollectionName)]
public sealed class LoginCaptchaTests : IDisposable
{
    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly WebApplicationFactory<Program> _guarded;
    private readonly HttpClient _http;
    private readonly HttpClient _guardedHttp;

    public LoginCaptchaTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Turnstile:Enabled"] = "true",
            ["Turnstile:SecretKey"] = "test-secret",
            ["Turnstile:AllowedHostnames:0"] = "www.obento.test",
        });
        _guarded = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<ITurnstileVerifier, FakeTurnstileVerifier>()));
        _guardedHttp = _guarded.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        _http = new ApiFactory(fixture.ConnectionString).CreateApiClient();
    }

    public void Dispose()
    {
        _guardedHttp.Dispose();
        _http.Dispose();
        _guarded.Dispose();
        _factory.Dispose();
    }

    private async Task<HttpResponseMessage> LoginAsync(string path, string email, string password, string? turnstile = null, bool admin = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { email, password }) };
        if (turnstile is not null)
        {
            request.Headers.Add(TurnstileFilter.Header, turnstile);
        }

        if (admin)
        {
            request.Headers.Add("Origin", ApiFactory.AdminOrigin);
            request.Headers.Add(TrustedOriginFilter.ClientHeader, "admin");
        }

        return await _guardedHttp.SendAsync(request);
    }

    [Fact]
    public async Task Khach_sai_3_lan_thi_lan_sau_can_Turnstile_ke_ca_email_khong_ton_tai()
    {
        const string Path = "/api/v1/auth/login";
        var email = AuthApiClient.NewEmail();
        (await new AuthApiClient(_http).RegisterAsync(email)).EnsureSuccessStatusCode();

        for (var i = 0; i < LoginGuard.CaptchaAfter; i++)
        {
            Assert.Equal(ErrorCodes.InvalidCredentials, await AuthApiClient.ReadErrorCodeAsync(await LoginAsync(Path, email, "Sai-mat-khau-1")));
        }

        Assert.Equal(ErrorCodes.CaptchaFailed, await AuthApiClient.ReadErrorCodeAsync(await LoginAsync(Path, email.ToUpperInvariant(), AuthApiClient.DefaultPassword)));
        Assert.Equal(ErrorCodes.CaptchaFailed, await AuthApiClient.ReadErrorCodeAsync(await LoginAsync(Path, email, AuthApiClient.DefaultPassword, "token-gia")));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(Path, email, AuthApiClient.DefaultPassword, FakeTurnstileVerifier.ValidToken)).StatusCode);

        // Đăng nhập đúng xóa bộ đếm: lần sau không cần Turnstile
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(Path, email, AuthApiClient.DefaultPassword)).StatusCode);

        // Email không tồn tại cũng bị yêu cầu như nhau (không lộ tài khoản)
        var ghost = AuthApiClient.NewEmail();
        for (var i = 0; i < LoginGuard.CaptchaAfter - 1; i++)
        {
            Assert.Equal(ErrorCodes.InvalidCredentials, await AuthApiClient.ReadErrorCodeAsync(await LoginAsync(Path, ghost, "Sai-mat-khau-1")));
        }

        Assert.Equal(ErrorCodes.InvalidCredentials, await AuthApiClient.ReadErrorCodeAsync(await LoginAsync(Path, ghost, "Sai-mat-khau-1")));
        Assert.Equal(ErrorCodes.CaptchaFailed, await AuthApiClient.ReadErrorCodeAsync(await LoginAsync(Path, ghost, "Sai-mat-khau-1")));
    }

    [Fact]
    public async Task Nhan_vien_sai_3_lan_thi_lan_sau_can_Turnstile()
    {
        const string Path = "/api/v1/admin/auth/login";
        var (_, email) = await new AdminAuthApiClient(_factory, _fixture).CreateStaffAsync(StaffRole.Cashier);

        for (var i = 0; i < LoginGuard.CaptchaAfter; i++)
        {
            Assert.Equal(ErrorCodes.InvalidCredentials, await AuthApiClient.ReadErrorCodeAsync(await LoginAsync(Path, email, "Sai-mat-khau-1", admin: true)));
        }

        Assert.Equal(ErrorCodes.CaptchaFailed, await AuthApiClient.ReadErrorCodeAsync(await LoginAsync(Path, email, AdminAuthApiClient.Password, admin: true)));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(Path, email, AdminAuthApiClient.Password, FakeTurnstileVerifier.ValidToken, admin: true)).StatusCode);
    }
}
