using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace ObentoTensho.IntegrationTests.Fixtures;

// Đặt bàn/đặt món chỉ dành cho tài khoản đã xác minh SĐT. Để các test nghiệp vụ không phải tự tạo tài khoản:
// yêu cầu tạo đơn/đặt bàn chưa có token thì đăng ký một khách mới (mỗi yêu cầu một khách, không dính giới hạn theo tài khoản);
// có token sẵn thì đánh dấu khách đó đã xác minh SĐT. Test cần gửi đúng như trình duyệt thì thêm header RawHeader
public sealed class VerifiedCustomerHandler(string connectionString) : DelegatingHandler
{
    public const string RawHeader = "X-Test-Raw";

    private static readonly string Password = $"Aa1!{Guid.NewGuid():N}";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Remove(RawHeader) || !IsBooking(request))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        if (request.Headers.Authorization is { } bearer)
        {
            await MarkVerifiedAsync(SubjectOf(bearer.Parameter!), phone: null, cancellationToken);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await NewCustomerAsync(request.RequestUri!, cancellationToken));
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static bool IsBooking(HttpRequestMessage request) =>
        request.Method == HttpMethod.Post && request.RequestUri is { } uri
            && (uri.AbsolutePath.Equals("/api/v1/orders", StringComparison.Ordinal) || uri.AbsolutePath.Equals("/api/v1/reservations", StringComparison.Ordinal));

    private async Task<string> NewCustomerAsync(Uri origin, CancellationToken cancellationToken)
    {
        var email = $"khach.{Guid.NewGuid():N}@example.com";
        var phone = NewPhone();
        using var register = new HttpRequestMessage(HttpMethod.Post, new Uri(origin, "/api/v1/auth/register"))
        {
            Content = JsonContent.Create(new { fullName = "Khách Test", email, phoneNumber = phone, password = Password, dateOfBirth = (string?)null, preferredLang = (string?)null }),
        };
        (await base.SendAsync(register, cancellationToken)).EnsureSuccessStatusCode();

        using var login = new HttpRequestMessage(HttpMethod.Post, new Uri(origin, "/api/v1/auth/login")) { Content = JsonContent.Create(new { email, password = Password }) };
        using var response = await base.SendAsync(login, cancellationToken);
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("data").GetProperty("accessToken").GetString()!;
        await MarkVerifiedAsync(SubjectOf(token), phone, cancellationToken);
        return token;
    }

    // Số trùng với khách đã xác minh khác (hiếm) thì đổi số khác
    private async Task MarkVerifiedAsync(Guid userId, string? phone, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        for (var attempt = 0; ; attempt++)
        {
            await using var command = connection.CreateCommand();
            // Khách tự tạo ở đây thì coi như đã xác thực cả email; khách của test chỉ được bật cờ SĐT
            command.CommandText = phone is null && attempt == 0
                ? "UPDATE [Users] SET [IsPhoneVerified] = 1 WHERE [Id] = @id"
                : "UPDATE [Users] SET [IsPhoneVerified] = 1, [IsEmailVerified] = 1, [PhoneNumber] = @phone WHERE [Id] = @id";
            command.Parameters.AddWithValue("@id", userId);
            command.Parameters.AddWithValue("@phone", "+84" + (phone ?? NewPhone())[1..]);
            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
                return;
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627 && attempt < 5)
            {
                phone = NewPhone();
            }
        }
    }

    private static string NewPhone() => "09" + Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);

    // Đọc "sub" trong JWT (không cần kiểm chữ ký: token do chính API test vừa cấp)
    private static Guid SubjectOf(string jwt)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        return Guid.Parse(json.RootElement.GetProperty("sub").GetString()!);
    }
}
