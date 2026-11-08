using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ObentoTensho.Api.Http;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Auth;

// Gọi API xác thực như frontend: cookie refresh gửi tay vì client test không lưu cookie Secure
public sealed class AuthApiClient(HttpClient http)
{
    public const string DefaultPassword = "Bento-thom-ngon-2026";

    public static string NewEmail() => $"khach.{Guid.NewGuid():N}@example.com";

    public Task<HttpResponseMessage> RegisterAsync(string email, string password = DefaultPassword) =>
        http.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "Nguyễn Văn An",
            email,
            phoneNumber = "0912 345 678",
            password,
            dateOfBirth = (string?)null,
            preferredLang = (string?)null,
        });

    public Task<HttpResponseMessage> LoginAsync(string email, string password = DefaultPassword) =>
        http.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    public Task<HttpResponseMessage> RefreshAsync(string? refreshToken, bool trusted = true) =>
        http.SendAsync(CookieRequest("/api/v1/auth/refresh-token", refreshToken, trusted));

    public Task<HttpResponseMessage> LogoutAsync(string? refreshToken) =>
        http.SendAsync(CookieRequest("/api/v1/auth/logout", refreshToken, trusted: true));

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return http.SendAsync(request);
    }

    public Task<HttpResponseMessage> GetProtectedAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test-probe");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return http.SendAsync(request);
    }

    public async Task<(string AccessToken, string RefreshToken)> RegisterAndLoginAsync(string email)
    {
        (await RegisterAsync(email)).EnsureSuccessStatusCode();
        var login = await LoginAsync(email);
        login.EnsureSuccessStatusCode();
        return (await ReadAccessTokenAsync(login), ReadRefreshCookie(login) ?? throw new InvalidOperationException("Thiếu cookie."));
    }

    public static async Task<string> ReadAccessTokenAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").GetProperty("accessToken").GetString() ?? string.Empty;
    }

    public static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    public static string? GetRefreshSetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(RefreshTokenCookie.CustomerName + "=", StringComparison.Ordinal))
            : null;

    public static string? ReadRefreshCookie(HttpResponseMessage response)
    {
        var header = GetRefreshSetCookie(response);
        if (header is null)
        {
            return null;
        }

        var value = header[(RefreshTokenCookie.CustomerName.Length + 1)..].Split(';')[0];
        return value.Length == 0 ? null : value;
    }

    private static HttpRequestMessage CookieRequest(string path, string? refreshToken, bool trusted)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (refreshToken is not null)
        {
            request.Headers.Add("Cookie", $"{RefreshTokenCookie.CustomerName}={refreshToken}");
        }

        if (trusted)
        {
            request.Headers.Add("Origin", ApiFactory.CustomerOrigin);
            request.Headers.Add(TrustedOriginFilter.ClientHeader, "web");
        }

        return request;
    }
}
