using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Infrastructure.Security;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;

namespace ObentoTensho.IntegrationTests.Content;

// docs/srs/17 §3.4, §3.5, BR-CNT-33: đánh giá sau đơn hoàn thành, form liên hệ
[Collection(SqlServerFixture.CollectionName)]
public sealed class ReviewContactTests : IDisposable
{
    private const string Admin = "/api/v1/admin";
    private const string Reviews = "/api/v1/reviews";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;
    private readonly AuthApiClient _auth;
    private readonly OrderApiClient _orders;

    public ReviewContactTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _admin = new AdminAuthApiClient(_factory, fixture);
        _auth = new AuthApiClient(_http);
        _orders = new OrderApiClient(_http);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    private async Task<OrderTestMenu> MenuAsync()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        return await OrderTestMenu.CreateAsync(_fixture);
    }

    private async Task<string> CustomerAsync() => (await _auth.RegisterAndLoginAsync(AuthApiClient.NewEmail())).AccessToken;

    // Đơn của khách, đã hoàn thành `daysAgo` ngày trước; null = chưa hoàn thành
    private async Task<Guid> OrderAsync(string token, int menuItemId, int optionId, int? daysAgo = 0)
    {
        var email = AuthApiClient.NewEmail();
        var created = await _orders.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menuItemId, 1, optionId)], email: email), token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        await using var db = _fixture.CreateContext();
        var id = await db.Orders.Where(o => o.CustomerEmail == email).Select(o => o.Id).SingleAsync();
        if (daysAgo is { } days)
        {
            var completedAt = DateTime.UtcNow.AddDays(-days);
            await db.Orders.Where(o => o.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Completed)
                .SetProperty(o => o.PaymentStatus, PaymentStatus.Paid)
                .SetProperty(o => o.PaidAtUtc, completedAt)
                .SetProperty(o => o.CompletedAtUtc, completedAt));
        }

        return id;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _http.SendAsync(request);
    }

    private Task<HttpResponseMessage> ReviewAsync(string token, Guid orderId, int? menuItemId, int rating = 5, string? comment = "Ngon") =>
        SendAsync(HttpMethod.Post, Reviews, token, new { orderId, menuItemId, rating, comment });

    private async Task<int> CreateReviewAsync(string token, Guid orderId, int? menuItemId, string? comment = "Ngon")
    {
        var response = await ReviewAsync(token, orderId, menuItemId, comment: comment);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await AdminAuthApiClient.ReadDataAsync(response)).GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement> ListAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");

    private async Task<JsonElement> AdminRowAsync(string token, int id)
    {
        var rows = await ListAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/reviews?pageSize=50", token));
        return rows.EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == id);
    }

    private static string Etag(JsonElement item) => $"\"{item.GetProperty("version").GetString()}\"";

    private async Task ApproveAsync(string staff, int id)
    {
        var row = await AdminRowAsync(staff, id);
        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/reviews/{id}/approve", staff, null, Etag(row))).StatusCode);
    }

    private async Task<bool> IsPublicAsync(int id, int menuItemId)
    {
        var rows = await ListAsync(await _http.GetAsync($"{Reviews}?menuItemId={menuItemId}&pageSize=50"));
        return rows.EnumerateArray().Any(r => r.GetProperty("id").GetInt32() == id);
    }

    [Fact]
    public async Task Chi_don_hoan_thanh_cua_chinh_minh_trong_30_ngay_va_mon_thuoc_don_moi_danh_gia_duoc()
    {
        var menu = await MenuAsync();
        var (owner, other) = (await CustomerAsync(), await CustomerAsync());

        var done = await OrderAsync(owner, menu.Rice, menu.Small, daysAgo: 29);
        var open = await OrderAsync(owner, menu.Rice, menu.Small, daysAgo: null);
        var old = await OrderAsync(owner, menu.Rice, menu.Small, daysAgo: 31);

        Assert.Equal(HttpStatusCode.NotFound, (await ReviewAsync(other, done, menu.Rice)).StatusCode);
        Assert.Equal(ErrorCodes.ReviewNotAllowed, await AuthApiClient.ReadErrorCodeAsync(await ReviewAsync(owner, open, menu.Rice)));
        Assert.Equal(ErrorCodes.ReviewNotAllowed, await AuthApiClient.ReadErrorCodeAsync(await ReviewAsync(owner, old, menu.Rice)));
        Assert.Equal(HttpStatusCode.BadRequest, (await ReviewAsync(owner, done, menu.Tea)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ReviewAsync(owner, done, menu.Rice, rating: 6)).StatusCode);

        await CreateReviewAsync(owner, done, menu.Rice);
        await CreateReviewAsync(owner, done, menuItemId: null);
        Assert.Equal(ErrorCodes.ReviewExists, await AuthApiClient.ReadErrorCodeAsync(await ReviewAsync(owner, done, menu.Rice)));
        Assert.Equal(ErrorCodes.ReviewExists, await AuthApiClient.ReadErrorCodeAsync(await ReviewAsync(owner, done, null)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await _http.PostAsJsonAsync(Reviews, new { orderId = done, menuItemId = menu.Rice, rating = 5, comment = "x" })).StatusCode);
    }

    [Fact]
    public async Task Cong_khai_chi_thay_danh_gia_da_duyet_voi_ten_rut_gon()
    {
        var menu = await MenuAsync();
        var customer = await CustomerAsync();
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var id = await CreateReviewAsync(customer, await OrderAsync(customer, menu.Rice, menu.Small), menu.Rice, "<b>Ngon</b>");
        Assert.False(await IsPublicAsync(id, menu.Rice));

        var approve = $"{Admin}/reviews/{id}/approve";
        var row = await AdminRowAsync(manager, id);
        Assert.Equal("Nguyễn Văn An", row.GetProperty("customerName").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Post, approve, cashier, null, Etag(row))).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await _admin.SendAsync(HttpMethod.Post, approve, manager)).StatusCode);

        await ApproveAsync(manager, id);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await _admin.SendAsync(HttpMethod.Post, approve, manager, null, Etag(row))).StatusCode);

        var reply = await _admin.SendAsync(HttpMethod.Post, $"{Admin}/reviews/{id}/reply", manager, new { content = "Cảm ơn bạn" }, Etag(await AdminRowAsync(manager, id)));
        Assert.Equal(HttpStatusCode.NoContent, reply.StatusCode);

        var raw = await (await _http.GetAsync($"{Reviews}?menuItemId={menu.Rice}&pageSize=50")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Nguyễn Văn An", raw, StringComparison.Ordinal);
        var shown = (await ListAsync(await _http.GetAsync($"{Reviews}?menuItemId={menu.Rice}&pageSize=50"))).EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == id);
        Assert.Equal("Nguyễn A.", shown.GetProperty("authorName").GetString());
        Assert.Equal("Cảm ơn bạn", shown.GetProperty("replyContent").GetString());
        Assert.Equal("<b>Ngon</b>", shown.GetProperty("comment").GetString());
        Assert.False(shown.TryGetProperty("userId", out _));

        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/reviews/{id}/reject", manager, null, Etag(await AdminRowAsync(manager, id)))).StatusCode);
        Assert.False(await IsPublicAsync(id, menu.Rice));
    }

    [Fact]
    public async Task Sua_sau_khi_duyet_quay_lai_cho_va_qua_24_gio_thi_khoa()
    {
        var menu = await MenuAsync();
        var (customer, other) = (await CustomerAsync(), await CustomerAsync());
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var id = await CreateReviewAsync(customer, await OrderAsync(customer, menu.Rice, menu.Small), menu.Rice);
        var path = $"{Reviews}/{id}";

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Put, path, other, new { rating = 1, comment = "Dở" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Delete, path, other)).StatusCode);

        await ApproveAsync(manager, id);
        Assert.True(await IsPublicAsync(id, menu.Rice));

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Put, path, customer, new { rating = 4, comment = "Khá" })).StatusCode);
        Assert.False(await IsPublicAsync(id, menu.Rice));

        // Quá 24 giờ, còn chờ duyệt ⇒ vẫn sửa được; đã duyệt ⇒ khóa
        await using (var db = _fixture.CreateContext())
        {
            var created = DateTime.UtcNow.AddHours(-25);
            await db.Reviews.Where(r => r.Id == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedAtUtc, created));
        }

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Put, path, customer, new { rating = 3, comment = "Tạm" })).StatusCode);
        await ApproveAsync(manager, id);
        Assert.Equal(ErrorCodes.ReviewLocked, await AuthApiClient.ReadErrorCodeAsync(await SendAsync(HttpMethod.Put, path, customer, new { rating = 1, comment = "x" })));
        Assert.Equal(ErrorCodes.ReviewLocked, await AuthApiClient.ReadErrorCodeAsync(await SendAsync(HttpMethod.Delete, path, customer)));

        var fresh = await CreateReviewAsync(customer, await OrderAsync(customer, menu.Rice, menu.Small), menu.Rice);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, $"{Reviews}/{fresh}", customer)).StatusCode);

        var mine = await AdminAuthApiClient.ReadDataAsync(await SendAsync(HttpMethod.Get, $"{Reviews}/mine", customer));
        var only = Assert.Single(mine.EnumerateArray());
        Assert.False(only.GetProperty("canEdit").GetBoolean());
    }

    [Fact]
    public async Task Moi_tai_khoan_toi_da_10_danh_gia_mot_ngay()
    {
        var menu = await MenuAsync();
        var customer = await CustomerAsync();
        for (var i = 0; i < 10; i++)
        {
            await CreateReviewAsync(customer, await OrderAsync(customer, menu.Rice, menu.Small), menu.Rice);
        }

        var over = await ReviewAsync(customer, await OrderAsync(customer, menu.Rice, menu.Small), menu.Rice);

        Assert.Equal(HttpStatusCode.TooManyRequests, over.StatusCode);
    }

    private static object Contact(string email, string message, string? website = null, string fullName = "Trần Bình") =>
        new { fullName, email, phoneNumber = "0901234567", subject = "Đặt tiệc", message, lang = "en", website };

    private Task<HttpResponseMessage> ContactAsync(object body) => _http.PostAsJsonAsync("/api/v1/contact", body);

    [Fact]
    public async Task Lien_he_luu_va_xep_thu_chong_trung_va_o_bay()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var email = AuthApiClient.NewEmail();
        var text = $"phong-rieng-{Guid.NewGuid():N}";

        Assert.Equal(HttpStatusCode.Accepted, (await ContactAsync(Contact(email, text))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await ContactAsync(Contact(email, text))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await ContactAsync(Contact(email, "bẫy", "https://spam.test"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ContactAsync(Contact("khong-phai-email", text))).StatusCode);

        await using (var db = _fixture.CreateContext())
        {
            var saved = await db.ContactMessages.Where(m => m.Email == email).ToListAsync();
            Assert.Equal(text, Assert.Single(saved).Message);

            var key = $"contact_received:{saved[0].Id}";
            var receipt = await db.OutboxMessages.SingleAsync(m => m.IdempotencyKey == key);
            Assert.Contains("\"language\":\"en\"", receipt.PayloadJson, StringComparison.Ordinal);
            Assert.DoesNotContain(text, receipt.PayloadJson, StringComparison.Ordinal);
            Assert.DoesNotContain("fullName", receipt.PayloadJson, StringComparison.Ordinal);

            var prefix = $"contact_internal:{saved[0].Id}:";
            var internalMails = await db.OutboxMessages.Where(m => m.IdempotencyKey.StartsWith(prefix)).ToListAsync();
            Assert.NotEmpty(internalMails);
            Assert.All(internalMails, m => Assert.Contains(text, m.PayloadJson, StringComparison.Ordinal));
        }

        var rows = await ListAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/contact-messages?unreadOnly=true&pageSize=50", manager));
        var row = rows.EnumerateArray().First(r => string.Equals(r.GetProperty("email").GetString(), email, StringComparison.Ordinal));
        var id = row.GetProperty("id").GetInt32();

        var read = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/contact-messages/{id}/read", manager));
        Assert.True(read.GetProperty("isRead").GetBoolean());
        var replied = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/contact-messages/{id}/replied", manager));
        var repliedAt = replied.GetProperty("repliedAtUtc").GetDateTime();
        var again = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/contact-messages/{id}/replied", manager));
        Assert.Equal(repliedAt, again.GetProperty("repliedAtUtc").GetDateTime(), TimeSpan.FromMilliseconds(1));

        var unread = await ListAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/contact-messages?unreadOnly=true&pageSize=50", manager));
        Assert.DoesNotContain(unread.EnumerateArray(), r => r.GetProperty("id").GetInt32() == id);

        var editor = await _admin.SignInAsync(StaffRole.ContentEditor);
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, $"{Admin}/contact-messages", editor)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/contact-messages/0/read", manager)).StatusCode);
    }

    [Fact]
    public async Task Lien_he_toi_da_3_tin_mot_gio_cho_moi_email()
    {
        var email = AuthApiClient.NewEmail();
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Accepted, (await ContactAsync(Contact(email, $"Tin {i}"))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await ContactAsync(Contact(email.ToUpperInvariant(), "Tin 4"))).StatusCode);
    }

    [Fact]
    public async Task Lien_he_bat_buoc_Turnstile_khi_bat()
    {
        using var factory = new ApiFactory(_fixture.ConnectionString, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Turnstile:Enabled"] = "true",
            ["Turnstile:SecretKey"] = "test-secret",
            ["Turnstile:AllowedHostnames:0"] = "www.obento.test",
        });
        using var http = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<ITurnstileVerifier, FakeTurnstileVerifier>()))
            .CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var missing = await http.PostAsJsonAsync("/api/v1/contact", Contact(AuthApiClient.NewEmail(), "Không có token"));

        Assert.Equal(ErrorCodes.CaptchaFailed, await AuthApiClient.ReadErrorCodeAsync(missing));
    }
}
