using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;

namespace ObentoTensho.IntegrationTests.Delivery;

// Mã giảm phí ship do quán tự đặt (free ship, giảm một phần): chỉ dùng cho đơn giao hàng, phí ship do server tính lại
[Collection(SqlServerFixture.CollectionName)]
public sealed class ShippingVoucherTests : IDisposable
{
    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly OrderApiClient _orders;
    private readonly AdminAuthApiClient _admin;

    public ShippingVoucherTests(SqlServerFixture fixture)
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

    private async Task<string> FreeShipAsync(string manager, int minOrderValue)
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = null, ["discountType"] = "percent", ["discountValue"] = 100, ["maxDiscountAmount"] = null,
            ["minOrderValue"] = minOrderValue, ["maxUsageTotal"] = 100, ["maxUsagePerUser"] = 1, ["applicableTier"] = null,
            ["requireLogin"] = false, ["startDate"] = null, ["endDate"] = null, ["isActive"] = true, ["appliesTo"] = "shipping",
        };
        var data = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, "/api/v1/admin/vouchers", manager, body));
        Assert.Equal("approved", data.GetProperty("approvalStatus").GetString());
        return data.GetProperty("code").GetString()!;
    }

    private static Dictionary<string, object?> Order(OrderTestMenu menu, string code, string? address)
    {
        var order = JsonSerializer.SerializeToElement(OrderApiClient.Order([OrderApiClient.Line(menu.Tea, 4)]));
        var body = order.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value, StringComparer.Ordinal);
        body["voucherCode"] = code;
        if (address is not null)
        {
            body["channel"] = "delivery";
            body["deliveryAddress"] = address;
        }

        return body;
    }

    [Fact]
    public async Task Free_ship_tru_het_phi_ship_va_khong_dung_cho_don_mang_ve()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var code = await FreeShipAsync(manager, minOrderValue: 20_000);

        // Xem trước ở giỏ: có phí ship thì giảm đúng phí ship, tiền món giữ nguyên
        var check = await (await _http.PostAsJsonAsync("/api/v1/vouchers/validate", new { code, items = new[] { OrderApiClient.Line(menu.Tea, 4) }, deliveryFee = 45_000 })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(45_000, check.GetProperty("data").GetProperty("shippingDiscount").GetDecimal());
        Assert.Equal(0, check.GetProperty("data").GetProperty("voucherDiscount").GetDecimal());

        // Đơn mang về không có phí ship ⇒ mã không dùng được
        Assert.Equal(ErrorCodes.VoucherShippingOnly, await AuthApiClient.ReadErrorCodeAsync(await _orders.CreateAsync(Order(menu, code, address: null))));

        var created = await _orders.CreateAsync(Order(menu, code, "88 Nguyễn Huệ 4100m Quận 1"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var order = (await OrderApiClient.DataOf(created)).GetProperty("order");
        Assert.Equal(45_000, order.GetProperty("deliveryFee").GetDecimal());
        Assert.Equal(45_000, order.GetProperty("shippingDiscount").GetDecimal());
        Assert.Equal(20_000, order.GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task Don_chua_dat_muc_toi_thieu_thi_khong_duoc_free_ship()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var code = await FreeShipAsync(manager, minOrderValue: 300_000);

        Assert.Equal(ErrorCodes.VoucherMinOrder, await AuthApiClient.ReadErrorCodeAsync(await _orders.CreateAsync(Order(menu, code, "88 Nguyễn Huệ 4100m Quận 1"))));
    }
}
