using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Orders;

// Gọi API đặt món công khai như trang khách
public sealed partial class OrderApiClient(HttpClient http)
{
    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string NewPhone() => "09" + Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);

    public static object Line(int menuItemId, int quantity, params int[] optionIds) =>
        new { menuItemId, quantity, optionIds, note = (string?)null };

    public static object Order(object[] items, string? phone = null, string? email = null, DateOnly? date = null, string time = "12:00", string method = "cash", string lang = "vi") => new
    {
        customerName = "Trần Thị Bình",
        customerPhone = phone ?? NewPhone(),
        customerEmail = email ?? $"khach.{Guid.NewGuid():N}@example.com",
        pickupDate = Date(date ?? TestSchedule.Tomorrow),
        pickupTime = time,
        paymentMethod = method,
        items,
        note = "Ít cay",
        lang,
    };

    public Task<HttpResponseMessage> CreateAsync(object order, string? customerToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders") { Content = JsonContent.Create(order) };
        if (customerToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        }

        return http.SendAsync(request);
    }

    public Task<HttpResponseMessage> QuoteAsync(object[] items, string lang = "vi") =>
        http.PostAsJsonAsync("/api/v1/orders/quote", new { items, lang });

    public Task<HttpResponseMessage> LookupAsync(string code, string last4) =>
        http.PostAsJsonAsync("/api/v1/orders/lookup", new { code, phoneLast4 = last4 });

    public Task<HttpResponseMessage> TrackAsync(string code, string token) =>
        http.PostAsJsonAsync("/api/v1/orders/track", new { code, token });

    public Task<HttpResponseMessage> CancelAsync(string code, string? token, string? reason = null) =>
        http.PostAsJsonAsync($"/api/v1/orders/{code}/cancel", new { token, reason });

    public Task<HttpResponseMessage> GetAsync(string path, string? bearer = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return http.SendAsync(request);
    }

    public static async Task<JsonElement> DataOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");

    // Token trong liên kết theo dõi của thư "đã nhận đơn"
    public static string TokenIn(string textBody) =>
        Uri.UnescapeDataString(TokenPattern().Match(textBody).Groups["token"].Value);

    [GeneratedRegex(@"#token=(?<token>[A-Za-z0-9_%\-]+)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TokenPattern();
}
