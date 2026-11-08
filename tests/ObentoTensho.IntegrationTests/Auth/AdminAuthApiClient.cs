using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Auth;

// Gọi API xác thực quản trị và chuẩn bị dữ liệu nhân viên trực tiếp trong CSDL test
public sealed class AdminAuthApiClient(ApiFactory factory, SqlServerFixture fixture)
{
    public const string Password = "Ca-lam-sang-2026";

    private readonly HttpClient _http = factory.CreateApiClient();

    public async Task<(Guid Id, string Email)> CreateStaffAsync(StaffRole role, bool mustChangePassword = false, string? passwordHash = null)
    {
        var hasher = factory.Services.GetRequiredService<IPasswordHasher>();
        var staff = new Staff
        {
            FullName = "Nhân Viên Thử",
            Email = $"nv.{Guid.NewGuid():N}@obento.test",
            PasswordHash = passwordHash ?? hasher.Hash(Password),
            Role = role,
            MustChangePassword = mustChangePassword,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTime.UtcNow,
        };

        await using var db = fixture.CreateContext();
        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync();
        return (staff.Id, staff.Email);
    }

    public async Task<byte[]> ReadTotpSecretAsync(Guid staffId)
    {
        await using var db = fixture.CreateContext();
        var encrypted = await db.StaffMembers.Where(s => s.Id == staffId).Select(s => s.TotpSecretEncrypted).SingleAsync();
        var protector = factory.Services.GetRequiredService<ITotpSecretProtector>();
        return protector.Unprotect(encrypted ?? []) ?? throw new InvalidOperationException("Chưa có khóa TOTP.");
    }

    public static string CurrentCode(byte[] secret, int stepOffset = 0) =>
        Totp.ComputeCode(secret, Totp.GetStep(DateTime.UtcNow) + stepOffset);

    public Task<HttpResponseMessage> PostAsync(string path, object body) =>
        _http.PostAsJsonAsync("/api/v1/admin/auth/" + path, body);

    public Task<HttpResponseMessage> LoginAsync(string email, string password = Password) =>
        PostAsync("login", new { email, password });

    // Tạo nhân viên vai trò `role` và đăng nhập hết các bước (tự thiết lập 2FA cho SuperAdmin/Manager); trả access token
    public async Task<string> SignInAsync(StaffRole role)
    {
        var (id, email) = await CreateStaffAsync(role);
        // Body chỉ đọc được một lần
        var data = await ReadDataAsync(await LoginAsync(email));
        var challenge = data.GetProperty("challenge");
        if (challenge.ValueKind == JsonValueKind.Null)
        {
            return data.GetProperty("session").GetProperty("accessToken").GetString() ?? string.Empty;
        }

        var token = challenge.GetProperty("challengeToken").GetString();
        (await PostAsync("2fa/setup", new { challengeToken = token })).EnsureSuccessStatusCode();
        var secret = await ReadTotpSecretAsync(id);
        var confirm = await PostAsync("2fa/setup/confirm", new { challengeToken = token, code = CurrentCode(secret) });
        confirm.EnsureSuccessStatusCode();
        return (await ReadDataAsync(confirm)).GetProperty("session").GetProperty("accessToken").GetString() ?? string.Empty;
    }

    // SEC-61: mã step-up 5 phút (nhập lại mật khẩu)
    public async Task<string> StepUpAsync(string accessToken)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/auth/step-up", accessToken, new { password = Password, code = (string?)null });
        return (await ReadDataAsync(response)).GetProperty("token").GetString()!;
    }

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string accessToken, object? body = null, string? ifMatch = null, string? stepUp = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (stepUp is not null)
        {
            request.Headers.Add("X-Step-Up-Token", stepUp);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return _http.SendAsync(request);
    }

    // Gửi tệp dạng multipart/form-data, trường "file"
    // chunked: không gửi Content-Length (server chỉ biết tệp quá lớn khi đang đọc)
    public Task<HttpResponseMessage> UploadAsync(string path, string accessToken, string fileName, byte[] content, bool chunked = false)
    {
        var form = new MultipartFormDataContent();
        HttpContent file = chunked ? new UnknownLengthContent(content) : new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", fileName);
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Server từ chối tệp quá lớn trước khi client gửi thân (Kestrel đóng kết nối nếu thân đang được gửi dở)
        request.Headers.ExpectContinue = true;
        return _http.SendAsync(request);
    }

    public Task<HttpResponseMessage> RefreshAsync(string? refreshToken, string origin = ApiFactory.AdminOrigin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/auth/refresh-token");
        if (refreshToken is not null)
        {
            request.Headers.Add("Cookie", $"{RefreshTokenCookie.AdminName}={refreshToken}");
        }

        request.Headers.Add("Origin", origin);
        request.Headers.Add(TrustedOriginFilter.ClientHeader, "admin");
        return _http.SendAsync(request);
    }

    public Task<HttpResponseMessage> GetAsync(string path, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _http.SendAsync(request);
    }

    public static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data");
    }

    // (loại bước, mã tạm) khi phản hồi là challenge; null khi đã có phiên
    public static async Task<(string Kind, string Token)?> ReadChallengeAsync(HttpResponseMessage response)
    {
        var data = await ReadDataAsync(response);
        var challenge = data.GetProperty("challenge");
        return challenge.ValueKind == JsonValueKind.Null
            ? null
            : (challenge.GetProperty("kind").GetString() ?? string.Empty, challenge.GetProperty("challengeToken").GetString() ?? string.Empty);
    }

    public static string? ReadAdminCookie(HttpResponseMessage response)
    {
        var prefix = RefreshTokenCookie.AdminName + "=";
        var header = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(prefix, StringComparison.Ordinal))
            : null;
        var value = header?[prefix.Length..].Split(';')[0];
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
            stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
