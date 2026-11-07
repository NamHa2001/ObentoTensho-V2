using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Orders;

// docs/srs/12 §11 (phía quán): gọi xác nhận trước khi bếp làm, quyền theo bước, thanh toán một lần, hoàn tiền, dữ liệu tối thiểu cho bếp
[Collection(SqlServerFixture.CollectionName)]
public sealed class OrderDeskTests : IDisposable
{
    private const string Admin = "/api/v1/admin/orders";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly OrderApiClient _orders;
    private readonly AdminAuthApiClient _admin;

    public OrderDeskTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _orders = new OrderApiClient(_http);
        _admin = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Khach_moi_phai_goi_xac_nhan_roi_bep_lam_thu_ngan_thu_tien_va_giao_mon()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var kitchen = await _admin.SignInAsync(StaffRole.Kitchen);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var email = AuthApiClient.NewEmail();
        var phone = OrderApiClient.NewPhone();
        var (id, code) = await CreateAsync(menu, phone, email);

        // Khách lần đầu ⇒ phải gọi; bếp chưa thấy và chưa làm được
        var listed = await FindInListAsync(cashier, code);
        Assert.True(listed.GetProperty("needsContactConfirm").GetBoolean());
        Assert.DoesNotContain(code, await KitchenCodesAsync(kitchen), StringComparer.Ordinal);
        Assert.Equal(ErrorCodes.ContactNotConfirmed, await ErrorOf(Status(kitchen, id, "pending", "preparing")));

        // Bếp không xem được danh sách có liên hệ, không hiện được liên hệ
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, Admin, kitchen)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/{id}/reveal-contact", kitchen)).StatusCode);

        var contact = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/{id}/reveal-contact", cashier));
        Assert.Equal("+84" + phone[1..], contact.GetProperty("phone").GetString());
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Patch, $"{Admin}/{id}/contact-confirmed", cashier, new { note = "Khách xác nhận lấy 12h" })).StatusCode);

        // Màn hình bếp: có đơn, không có SĐT/email/tiền, tên gọi ngắn, tùy chọn tiếng Việt
        var card = await KitchenCardAsync(kitchen, code);
        Assert.Equal("Bình", card.GetProperty("shortName").GetString());
        Assert.False(card.TryGetProperty("maskedPhone", out _));
        Assert.False(card.TryGetProperty("totalAmount", out _));
        Assert.Equal("Size: Lớn", card.GetProperty("items")[0].GetProperty("options")[0].GetString());

        Assert.Equal(HttpStatusCode.OK, (await Status(kitchen, id, "pending", "preparing")).StatusCode);
        Assert.Equal(ErrorCodes.InvalidTransition, await ErrorOf(Status(kitchen, id, "pending", "ready")));
        Assert.Equal(HttpStatusCode.OK, (await Status(kitchen, id, "preparing", "ready")).StatusCode);

        // Bếp không giao món; thu ngân giao khi chưa thu tiền bị chặn (BR-ORD-10)
        Assert.Equal(HttpStatusCode.Forbidden, (await Status(kitchen, id, "ready", "completed")).StatusCode);
        Assert.Equal(ErrorCodes.PaymentRequired, await ErrorOf(Status(cashier, id, "ready", "completed")));

        // Hai thu ngân bấm "đã thu tiền" cùng lúc: cả hai thấy đã thu, chỉ ghi một lần (BR-ORD-09)
        var paid = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _admin.SendAsync(HttpMethod.Patch, $"{Admin}/{id}/payment", cashier)));
        Assert.All(paid, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(HttpStatusCode.OK, (await Status(cashier, id, "ready", "completed")).StatusCode);

        await AssertPaidOnceWithFullHistoryAsync(id);

        // Thư "sẵn sàng" gửi cả khách không có tài khoản (BR-ORD-12)
        await _factory.ProcessOutboxAsync();
        Assert.Equal(2, _factory.Emails.To(email).Count);

        // Đã có đơn hoàn thành ⇒ đơn sau không cần gọi; đơn tiền mặt từ 500.000 ₫ vẫn phải gọi
        var (_, second) = await CreateAsync(menu, phone, email);
        Assert.False((await FindInListAsync(cashier, second)).GetProperty("needsContactConfirm").GetBoolean());
        var (_, large) = await CreateAsync(menu, phone, email, quantity: 7);
        Assert.True((await FindInListAsync(cashier, large)).GetProperty("needsContactConfirm").GetBoolean());
    }

    [Fact]
    public async Task Huy_don_da_thu_tien_phai_ghi_hoan_tien_va_khong_nhan_thanh_toan_sau_khi_huy()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var kitchen = await _admin.SignInAsync(StaffRole.Kitchen);
        var email = AuthApiClient.NewEmail();
        var (id, code) = await CreateAsync(menu, OrderApiClient.NewPhone(), email);

        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Patch, $"{Admin}/{id}/payment", cashier)).StatusCode);

        // Hủy bắt buộc lý do; bếp hủy được (quyền hủy)
        Assert.Equal(HttpStatusCode.BadRequest, (await Status(kitchen, id, "pending", "cancelled")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Status(kitchen, id, "pending", "cancelled", "Hết nguyên liệu")).StatusCode);
        Assert.Equal(ErrorCodes.InvalidTransition, await ErrorOf(Status(kitchen, id, "cancelled", "cancelled", "Lần hai")));

        var pending = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}?refundPending=true&q={code}", cashier));
        Assert.True(Assert.Single(pending.EnumerateArray()).GetProperty("refundPending").GetBoolean());

        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Patch, $"{Admin}/{id}/refund", cashier, new { note = "  " })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Patch, $"{Admin}/{id}/refund", cashier, new { note = "Đã chuyển khoản lại" })).StatusCode);
        Assert.Equal(ErrorCodes.InvalidTransition, await ErrorOf(_admin.SendAsync(HttpMethod.Patch, $"{Admin}/{id}/refund", cashier, new { note = "Lần hai" })));
        Assert.Equal(0, (await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}?refundPending=true&q={code}", cashier))).GetArrayLength());

        // Đơn đã hủy không nhận thanh toán (đơn khác, chưa thu tiền)
        var (other, _) = await CreateAsync(menu, OrderApiClient.NewPhone(), AuthApiClient.NewEmail());
        Assert.Equal(HttpStatusCode.OK, (await Status(cashier, other, "pending", "cancelled", "Khách gọi hủy")).StatusCode);
        Assert.Equal(ErrorCodes.InvalidTransition, await ErrorOf(_admin.SendAsync(HttpMethod.Patch, $"{Admin}/{other}/payment", cashier)));

        await _factory.ProcessOutboxAsync();
        var mails = _factory.Emails.To(email);
        Assert.Contains(mails, m => m.TextBody.Contains("Hết nguyên liệu", StringComparison.Ordinal));
        Assert.Contains(mails, m => m.TextBody.Contains("Đã chuyển khoản lại", StringComparison.Ordinal));

        var detail = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/{id}", cashier));
        Assert.Equal("Hết nguyên liệu", detail.GetProperty("cancelReason").GetString());
        Assert.Equal(2, detail.GetProperty("history").GetArrayLength());
    }

    private async Task AssertPaidOnceWithFullHistoryAsync(Guid id)
    {
        await using var db = _fixture.CreateContext();
        var idText = id.ToString("D", CultureInfo.InvariantCulture);
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "orders.order.paid" && a.EntityId == idText));
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "orders.order.contact_revealed" && a.EntityId == idText));
        var history = await db.OrderStatusHistory.Where(h => h.OrderId == id).OrderBy(h => h.Id).Select(h => h.ToStatus).ToListAsync();
        Assert.Equal([OrderStatus.Pending, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Completed], history);
    }

    private async Task<(Guid Id, string Code)> CreateAsync(OrderTestMenu menu, string phone, string email, int quantity = 1)
    {
        var created = await _orders.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Rice, quantity, menu.Large)], phone: phone, email: email));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var code = (await OrderApiClient.DataOf(created)).GetProperty("order").GetProperty("code").GetString()!;
        await using var db = _fixture.CreateContext();
        return (await db.Orders.Where(o => o.OrderCode == code).Select(o => o.Id).SingleAsync(), code);
    }

    private Task<HttpResponseMessage> Status(string token, Guid id, string expected, string status, string? reason = null) =>
        _admin.SendAsync(HttpMethod.Patch, $"{Admin}/{id}/status", token, new { expectedStatus = expected, status, reason });

    private async Task<JsonElement> FindInListAsync(string token, string code)
    {
        var list = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}?q={code}", token));
        return Assert.Single(list.EnumerateArray());
    }

    private async Task<string?[]> KitchenCodesAsync(string token) =>
        (await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/kitchen-view", token)))
            .EnumerateArray().Select(o => o.GetProperty("code").GetString()).ToArray();

    private async Task<JsonElement> KitchenCardAsync(string token, string code) =>
        (await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/kitchen-view", token)))
            .EnumerateArray().Single(o => string.Equals(o.GetProperty("code").GetString(), code, StringComparison.Ordinal));

    private static async Task<string?> ErrorOf(Task<HttpResponseMessage> request) =>
        await AuthApiClient.ReadErrorCodeAsync(await request);
}
