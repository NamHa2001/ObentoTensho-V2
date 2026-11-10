using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Orders;

// docs/srs/12 §11 (phía khách): giá do server tính, tùy chọn, giờ lấy, theo dõi, tự hủy, giới hạn theo SĐT
[Collection(SqlServerFixture.CollectionName)]
public sealed class PublicOrderTests : IDisposable
{
    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly OrderApiClient _api;

    public PublicOrderTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _api = new OrderApiClient(_http);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Dat_mon_gia_tu_CSDL_kem_tuy_chon_va_thu_xac_nhan_theo_ngon_ngu()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var email = AuthApiClient.NewEmail();
        var phone = "091" + Random.Shared.Next(1000, 9999).ToString(System.Globalization.CultureInfo.InvariantCulture) + "678";

        // Client gửi kèm trường giá ⇒ bị từ chối, không có chuyện "tự đặt giá" (BR-ORD-22)
        var withPrice = new { menuItemId = menu.Rice, quantity = 1, optionIds = new[] { menu.Small }, note = (string?)null, price = 1 };
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.CreateAsync(OrderApiClient.Order([withPrice]))).StatusCode);

        var created = await _api.CreateAsync(OrderApiClient.Order(
            [OrderApiClient.Line(menu.Rice, 2, menu.Large), OrderApiClient.Line(menu.Tea, 1, menu.Egg)],
            phone: phone,
            email: email,
            method: "bank_transfer",
            lang: "en"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var data = await OrderApiClient.DataOf(created);
        var order = data.GetProperty("order");

        // (65.000 + 15.000) × 2 + (5.000 + 5.000) × 1
        Assert.Equal(170_000m, order.GetProperty("totalAmount").GetDecimal());
        Assert.Equal("pending", order.GetProperty("status").GetString());
        Assert.Equal("091****678", order.GetProperty("maskedPhone").GetString());
        Assert.Equal("Chicken rice", order.GetProperty("items")[0].GetProperty("name").GetString());
        Assert.Equal("Large", order.GetProperty("items")[0].GetProperty("options")[0].GetProperty("optionName").GetString());
        Assert.True(order.GetProperty("canCancel").GetBoolean());
        var code = order.GetProperty("code").GetString()!;
        var token = data.GetProperty("accessToken").GetString()!;

        // Bếp đọc tiếng Việt: bản chụp giữ cả tên Việt
        await using (var db = _fixture.CreateContext())
        {
            var saved = await db.Orders.AsNoTracking().Include(o => o.Items).ThenInclude(i => i.Options).SingleAsync(o => o.OrderCode == code);
            var rice = saved.Items.Single(i => i.MenuItemId == menu.Rice);
            Assert.StartsWith("Rice ", rice.ItemName, StringComparison.Ordinal);
            Assert.Equal(("Lớn", 15_000m), (rice.Options.Single().OptionName, rice.Options.Single().PriceDelta));
        }

        await _factory.ProcessOutboxAsync();
        var mail = Assert.Single(_factory.Emails.To(email));
        Assert.Contains(code, mail.Subject, StringComparison.Ordinal);
        Assert.Contains("2 × Chicken rice (Large) — 160.000 ₫", mail.TextBody, StringComparison.Ordinal);
        Assert.Contains("Bank transfer", mail.TextBody, StringComparison.Ordinal);
        Assert.Equal(token, OrderApiClient.TokenIn(mail.TextBody));

        Assert.Equal(HttpStatusCode.OK, (await _api.TrackAsync(code.ToLowerInvariant(), token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.TrackAsync(code, "token-sai")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.LookupAsync(code, phone[^4..])).StatusCode);
        Assert.Equal(ErrorCodes.NotFound, await AuthApiClient.ReadErrorCodeAsync(await _api.LookupAsync(code, "0000")));
    }

    [Fact]
    public async Task Mon_het_khong_ban_mang_ve_hoac_tuy_chon_sai_bao_dung_dong()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);

        var quote = await OrderApiClient.DataOf(await _api.QuoteAsync(
        [
            OrderApiClient.Line(menu.Rice, 1, menu.Small),
            OrderApiClient.Line(menu.SoldOut, 1),
            OrderApiClient.Line(menu.DineInOnly, 1),
            OrderApiClient.Line(menu.Rice, 1),
            OrderApiClient.Line(menu.Tea, 1, menu.Cheese),
            OrderApiClient.Line(menu.Tea, 1, menu.Small),
        ]));
        Assert.False(quote.GetProperty("isValid").GetBoolean());
        Assert.Equal(
            [null, ErrorCodes.ItemUnavailable, ErrorCodes.ItemUnavailable, ErrorCodes.OptionInvalid, ErrorCodes.OptionUnavailable, ErrorCodes.OptionInvalid],
            quote.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("errorCode").GetString()),
            StringComparer.Ordinal);
        Assert.Equal(65_000m, quote.GetProperty("subTotal").GetDecimal());

        var rejected = await _api.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Rice, 1, menu.Small), OrderApiClient.Line(menu.SoldOut, 1)]));
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ErrorCodes.ItemUnavailable, problem.GetProperty("errorCode").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("items[1]", out _));

        Assert.Equal(HttpStatusCode.BadRequest, (await _api.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Tea, 100)]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.CreateAsync(OrderApiClient.Order([]))).StatusCode);
    }

    [Fact]
    public async Task Gio_lay_da_qua_ngoai_gio_mo_cua_qua_xa_hoac_bi_khoa_bi_tu_choi()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var tea = new[] { OrderApiClient.Line(menu.Tea, 1) };

        // Mở 06:00–23:00: giờ lấy cuối là 22:30 (trước giờ đóng 30 phút)
        Assert.Equal(ErrorCodes.DateOutOfRange, await ErrorOf(OrderApiClient.Order(tea, date: TestSchedule.TodayVn.AddDays(-1))));
        Assert.Equal(ErrorCodes.DateOutOfRange, await ErrorOf(OrderApiClient.Order(tea, date: TestSchedule.TodayVn.AddDays(8))));
        Assert.Equal(ErrorCodes.OutsideOpeningHours, await ErrorOf(OrderApiClient.Order(tea, time: "02:00")));
        Assert.Equal(ErrorCodes.OutsideOpeningHours, await ErrorOf(OrderApiClient.Order(tea, time: "22:45")));
        Assert.Equal(HttpStatusCode.Created, (await _api.CreateAsync(OrderApiClient.Order(tea, time: "22:30"))).StatusCode);

        var times = await OrderApiClient.DataOf(await _http.GetAsync($"/api/v1/orders/pickup-times?date={OrderApiClient.Date(TestSchedule.Tomorrow)}"));
        var list = times.GetProperty("times").EnumerateArray().Select(t => t.GetString()).ToArray();
        Assert.Equal(("06:00", "22:30", 67), (list[0], list[^1], list.Length));

        // Thiếu ngày thì báo lỗi, không lặng lẽ hiểu thành 0001-01-01
        Assert.Equal(HttpStatusCode.BadRequest, (await _http.GetAsync("/api/v1/orders/pickup-times")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _http.GetAsync("/api/v1/reservations/availability")).StatusCode);

        // Khóa nhận đơn 11:00–13:00 ngày mai (BR-TBL-18)
        var (staffId, _) = await new AdminAuthApiClient(_factory, _fixture).CreateStaffAsync(Contracts.Enums.StaffRole.Manager);
        await using (var db = _fixture.CreateContext())
        {
            db.BlockedDates.Add(new Core.Tables.Domain.BlockedDate
            {
                Date = TestSchedule.Tomorrow, AppliesTo = Contracts.Enums.BlockScope.Order, StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(13, 0),
                CreatedByStaffId = staffId, CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        Assert.Equal(ErrorCodes.DateBlocked, await ErrorOf(OrderApiClient.Order(tea, time: "12:00")));
        Assert.Equal(HttpStatusCode.Created, (await _api.CreateAsync(OrderApiClient.Order(tea, time: "13:00"))).StatusCode);
    }

    [Fact]
    public async Task Khach_tu_huy_khi_con_cho_va_moi_SDT_toi_da_3_don_dang_xu_ly()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var phone = OrderApiClient.NewPhone();
        var tea = new[] { OrderApiClient.Line(menu.Tea, 1) };

        var first = await OrderApiClient.DataOf(await _api.CreateAsync(OrderApiClient.Order(tea, phone: phone)));
        await _api.CreateAsync(OrderApiClient.Order(tea, phone: phone));
        await _api.CreateAsync(OrderApiClient.Order(tea, phone: phone));
        Assert.Equal(ErrorCodes.OrderLimit, await ErrorOf(OrderApiClient.Order(tea, phone: phone)));

        var code = first.GetProperty("order").GetProperty("code").GetString()!;
        var token = first.GetProperty("accessToken").GetString()!;
        Assert.Equal(HttpStatusCode.NotFound, (await _api.CancelAsync(code, "token-sai")).StatusCode);
        var cancelled = await _api.CancelAsync(code, token);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("cancelled", (await OrderApiClient.DataOf(cancelled)).GetProperty("status").GetString());
        Assert.Equal(ErrorCodes.InvalidTransition, await AuthApiClient.ReadErrorCodeAsync(await _api.CancelAsync(code, token)));

        // Hủy xong thì đặt được đơn mới
        Assert.Equal(HttpStatusCode.Created, (await _api.CreateAsync(OrderApiClient.Order(tea, phone: phone))).StatusCode);
    }

    [Fact]
    public async Task Khach_dang_nhap_chi_xem_duoc_don_cua_minh()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var auth = new AuthApiClient(_http);
        var (mine, _) = await auth.RegisterAndLoginAsync(AuthApiClient.NewEmail());
        var (other, _) = await auth.RegisterAndLoginAsync(AuthApiClient.NewEmail());

        var created = await OrderApiClient.DataOf(await _api.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Tea, 2)]), mine));
        var code = created.GetProperty("order").GetProperty("code").GetString()!;

        var list = await _api.GetAsync("/api/v1/account/orders", mine);
        var orders = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Contains(orders.EnumerateArray(), o => string.Equals(o.GetProperty("code").GetString(), code, StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync($"/api/v1/account/orders/{code}", mine)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync($"/api/v1/account/orders/{code}", other)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.GetAsync("/api/v1/account/orders")).StatusCode);

        // Chủ tài khoản hủy được không cần token
        var cancel = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/orders/{code}/cancel") { Content = JsonContent.Create(new { token = (string?)null, reason = (string?)null }) };
        cancel.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", mine);
        Assert.Equal(HttpStatusCode.OK, (await _http.SendAsync(cancel)).StatusCode);
    }

    private async Task<string?> ErrorOf(object order) =>
        await AuthApiClient.ReadErrorCodeAsync(await _api.CreateAsync(order));
}
