using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;

namespace ObentoTensho.IntegrationTests.Delivery;

// Đơn giao tận nơi: phí ship server tính lại và cộng vào tổng; bếp làm như đơn mang về; thu ngân chuyển "Đang giao"
// rồi "Đã giao" (phải thu tiền trước); không bỏ qua bước đang giao; điểm thưởng không tính tiền ship
[Collection(SqlServerFixture.CollectionName)]
public sealed class DeliveryOrderTests : IDisposable
{
    private const string Admin = "/api/v1/admin/orders";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly OrderApiClient _orders;
    private readonly AdminAuthApiClient _admin;

    public DeliveryOrderTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = DeliveryTestHost.Create(fixture.ConnectionString, new DeliveryTestHost.FakeRoutes());
        _http = _factory.CreateApiClient();
        _orders = new OrderApiClient(_http);
        _admin = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    private static Dictionary<string, object?> Body(OrderTestMenu menu, string? address)
    {
        var order = JsonSerializer.SerializeToElement(OrderApiClient.Order([OrderApiClient.Line(menu.Tea, 2)]));
        var body = order.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value, StringComparer.Ordinal);
        body["channel"] = "delivery";
        body["deliveryAddress"] = address;
        return body;
    }

    private Task<HttpResponseMessage> Status(string token, Guid id, string expected, string status) =>
        _admin.SendAsync(HttpMethod.Patch, $"{Admin}/{id}/status", token, new { expectedStatus = expected, status, reason = (string?)null });

    [Fact]
    public async Task Dat_giao_hang_tinh_phi_ship_va_di_qua_buoc_dang_giao()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var kitchen = await _admin.SignInAsync(StaffRole.Kitchen);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);

        // Thiếu địa chỉ hoặc ngoài phạm vi thì không tạo đơn
        Assert.Equal(ErrorCodes.ValidationFailed, await AuthApiClient.ReadErrorCodeAsync(await _orders.CreateAsync(Body(menu, null))));
        Assert.Equal(ErrorCodes.DeliveryTooFar, await AuthApiClient.ReadErrorCodeAsync(await _orders.CreateAsync(Body(menu, "rất xa 9000m Bình Chánh"))));

        var created = await _orders.CreateAsync(Body(menu, "88 Nguyễn Huệ 4100m Quận 1"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var order = (await OrderApiClient.DataOf(created)).GetProperty("order");
        Assert.Equal("delivery", order.GetProperty("channel").GetString());
        Assert.Equal(45_000, order.GetProperty("deliveryFee").GetDecimal());
        Assert.Equal(10_000 + 45_000, order.GetProperty("totalAmount").GetDecimal());
        var id = order.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Patch, $"{Admin}/{id}/contact-confirmed", cashier, new { note = "Đã gọi" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Status(kitchen, id, "pending", "preparing")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Status(kitchen, id, "preparing", "ready")).StatusCode);

        // Không bỏ qua bước đang giao; bếp không chuyển sang đang giao
        Assert.Equal(ErrorCodes.InvalidTransition, await AuthApiClient.ReadErrorCodeAsync(await Status(cashier, id, "ready", "completed")));
        Assert.Equal(HttpStatusCode.Forbidden, (await Status(kitchen, id, "ready", "delivering")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Status(cashier, id, "ready", "delivering")).StatusCode);

        // Giao xong phải thu tiền (tiền mặt khi nhận hàng) rồi mới hoàn thành
        Assert.Equal(ErrorCodes.PaymentRequired, await AuthApiClient.ReadErrorCodeAsync(await Status(cashier, id, "delivering", "completed")));
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Patch, $"{Admin}/{id}/payment", cashier)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Status(cashier, id, "delivering", "completed")).StatusCode);

        var detail = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/{id}", cashier));
        Assert.Equal("88 Nguyễn Huệ 4100m Quận 1", detail.GetProperty("deliveryAddress").GetString());
        Assert.Equal(4.1m, detail.GetProperty("deliveryDistanceKm").GetDecimal());

        // Đơn giao hàng không gửi thư "món đã sẵn sàng, mời đến lấy"
        await using var db = _fixture.CreateContext();
        var email = await db.Orders.Where(o => o.Id == id).Select(o => o.CustomerEmail).SingleAsync();
        var payloads = await db.OutboxMessages.Where(m => m.PayloadJson.Contains(email)).Select(m => m.PayloadJson).ToListAsync();
        Assert.NotEmpty(payloads);
        Assert.DoesNotContain(payloads, p => p.Contains("OrderReady", StringComparison.OrdinalIgnoreCase) || p.Contains("order_ready", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Khach_khong_tu_tao_don_tai_ban()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var body = Body(menu, null);
        body["channel"] = "dine_in";

        Assert.Equal(ErrorCodes.ValidationFailed, await AuthApiClient.ReadErrorCodeAsync(await _orders.CreateAsync(body)));
    }
}
