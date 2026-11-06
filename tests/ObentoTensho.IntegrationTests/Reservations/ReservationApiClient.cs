using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Reservations;

// Gọi API đặt bàn công khai như trang khách
public sealed partial class ReservationApiClient(HttpClient http)
{
    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string NewPhone() => "09" + Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);

    public static object Booking(DateOnly date, string time = "12:00", string? phone = null, int adults = 2, string? email = null) => new
    {
        date = Date(date),
        time,
        adultCount = adults,
        childCount = 0,
        guestName = "Trần Thị Bình",
        guestPhone = phone ?? NewPhone(),
        guestEmail = email ?? $"khach.{Guid.NewGuid():N}@example.com",
        specialRequest = "Bàn gần cửa sổ",
        lang = "vi",
    };

    public Task<HttpResponseMessage> CreateAsync(object booking) => http.PostAsJsonAsync("/api/v1/reservations", booking);

    public Task<HttpResponseMessage> LookupAsync(string code, string last4) =>
        http.PostAsJsonAsync("/api/v1/reservations/lookup", new { code, phoneLast4 = last4 });

    public Task<HttpResponseMessage> CancelAsync(string code, string? token) =>
        http.PostAsJsonAsync($"/api/v1/reservations/{code}/cancel", new { token, reason = (string?)null });

    public async Task<JsonElement> AvailabilityAsync(DateOnly date) =>
        (await http.GetFromJsonAsync<JsonElement>($"/api/v1/reservations/availability?date={Date(date)}")).GetProperty("data");

    public static async Task<string> CodeOf(HttpResponseMessage created) =>
        (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("code").GetString() ?? string.Empty;

    // Token trong liên kết quản lý đặt bàn của thư "đã nhận"
    public static string TokenIn(FakeEmailSender emails, string email) =>
        Uri.UnescapeDataString(TokenPattern().Match(emails.To(email).Single().TextBody).Groups["token"].Value);

    [GeneratedRegex(@"#token=(?<token>[A-Za-z0-9_%\-]+)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TokenPattern();
}
