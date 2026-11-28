using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Orders;

// docs/srs/23 §9: đơn app nhập lại từng món, đã thanh toán qua app, vào bếp như đơn tại quầy;
// ra hết món thì hoàn thành và chụp doanh thu ròng, khấu trừ 25%, GTGT 3%, TNCN 1,5%, thực nhận
[Collection(SqlServerFixture.CollectionName)]
public sealed class PlatformOrderTests : IDisposable
{
    private const string Api = "/api/v1/admin/platform-orders";
    private const string DineIn = "/api/v1/admin/dine-in";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly AdminAuthApiClient _admin;

    public PlatformOrderTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _admin = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose() => _factory.Dispose();

    private static async Task<JsonElement> OkAsync(Task<HttpResponseMessage> response)
    {
        var message = await response;
        Assert.True(message.IsSuccessStatusCode, await message.Content.ReadAsStringAsync());
        return await AdminAuthApiClient.ReadDataAsync(message);
    }

    private static object Body(OrderTestMenu menu, string group, string platform, string code, decimal? percent = null, decimal? amount = null) => new
    {
        paymentGroup = group,
        platform,
        externalOrderCode = code,
        discountPercent = percent,
        discountAmount = amount,
        note = (string?)null,
        items = new[] { OrderApiClient.Line(menu.Tea, 2), OrderApiClient.Line(menu.DineInOnly, 1) },
    };

    private static string Code() => "GF-" + Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task Don_grab_giam_15_phan_tram_ra_het_mon_thi_hoan_thanh_voi_thuc_nhan_59925()
    {
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var kitchen = await _admin.SignInAsync(StaffRole.Kitchen);

        var order = await OkAsync(_admin.SendAsync(HttpMethod.Post, Api, cashier, Body(menu, "tensho", "grab", Code(), percent: 15)));
        var id = order.GetProperty("id").GetGuid();
        Assert.Equal((100_000m, 15_000m, 85_000m, 59_925m), (order.GetProperty("subTotal").GetDecimal(), order.GetProperty("discount").GetDecimal(), order.GetProperty("netRevenue").GetDecimal(), order.GetProperty("receivedAmount").GetDecimal()));

        // Vào bếp như đơn tại quầy; ra hết món thì hoàn thành
        var ticket = (await OkAsync(_admin.SendAsync(HttpMethod.Get, $"{DineIn}/kitchen-view", kitchen))).EnumerateArray().Single(t => t.GetProperty("billId").GetGuid() == id);
        Assert.StartsWith("App · ", ticket.GetProperty("tableName").GetString(), StringComparison.Ordinal);
        foreach (var line in ticket.GetProperty("items").EnumerateArray())
        {
            await OkAsync(_admin.SendAsync(HttpMethod.Post, $"{DineIn}/{id}/items/{line.GetProperty("id").GetInt64()}/served", kitchen, new { }));
        }

        await using var db = _fixture.CreateContext();
        var saved = await db.Orders.SingleAsync(o => o.Id == id);
        Assert.Equal((OrderStatus.Completed, PaymentStatus.Paid, OrderChannel.Platform, PaymentGroup.Tensho, Platform.Grab), (saved.Status, saved.PaymentStatus, saved.Channel, saved.PaymentGroup, saved.Platform!.Value));
        Assert.Equal((85_000m, 21_250m, 2_550m, 1_275m, 59_925m), (saved.Revenue!.NetRevenue, saved.Revenue.DeductionAmount, saved.Revenue.VatAmount, saved.Revenue.PitAmount, saved.Revenue.ReceivedAmount));
        Assert.Equal((25m, 3m, 1.5m), (saved.Revenue.DeductionPercent, saved.Revenue.VatPercent, saved.Revenue.PitPercent));
    }

    [Fact]
    public async Task Chi_tiet_don_app_tra_ve_so_tien_giam_tren_app_de_tam_tinh_khop_tong()
    {
        // Chủ quán 2026-11-28: chi tiết đơn ghi Tạm tính 100.000 mà Tổng 85.000, không thấy khoản trừ
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var id = (await OkAsync(_admin.SendAsync(HttpMethod.Post, Api, cashier, Body(menu, "tensho", "grab", Code(), percent: 15)))).GetProperty("id").GetGuid();

        var detail = await OkAsync(_admin.SendAsync(HttpMethod.Get, $"/api/v1/admin/orders/{id}", cashier));
        Assert.True(detail.TryGetProperty("platformDiscount", out var discount), detail.ToString());
        Assert.Equal(15_000m, discount.GetDecimal());
        Assert.Equal(detail.GetProperty("subTotal").GetDecimal() - discount.GetDecimal(), detail.GetProperty("order").GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task Trung_ma_cung_nhom_cung_kenh_bi_chan_khac_kenh_thi_duoc_khong_phan_biet_hoa_thuong()
    {
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var code = Code();

        await OkAsync(_admin.SendAsync(HttpMethod.Post, Api, cashier, Body(menu, "hima", "shopee", code)));
        var duplicate = await _admin.SendAsync(HttpMethod.Post, Api, cashier, Body(menu, "hima", "shopee", "  " + code.ToLowerInvariant() + " "));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains(FieldErrorCodes.Duplicate, await duplicate.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await OkAsync(_admin.SendAsync(HttpMethod.Post, Api, cashier, Body(menu, "hima", "grab", code)));
    }

    [Theory]
    [InlineData("tensho", "be")]
    [InlineData("xanh", "grab")]
    [InlineData("in_store", "grab")]
    public async Task Kenh_con_sai_nhom_bi_chan(string group, string platform)
    {
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);

        var response = await _admin.SendAsync(HttpMethod.Post, Api, cashier, Body(menu, group, platform, Code()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Nhap_so_tien_giam_thi_hien_phan_tram_quan_ly_sua_giam_gia_thi_tinh_lai()
    {
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var manager = await _admin.SignInAsync(StaffRole.Manager);

        var order = await OkAsync(_admin.SendAsync(HttpMethod.Post, Api, cashier, Body(menu, "xanh", "xanh", Code(), amount: 30_000)));
        Assert.Equal(30m, order.GetProperty("discountPercent").GetDecimal());
        var id = order.GetProperty("id").GetGuid();

        // Thu ngân không sửa được; quản lý sửa giảm còn 10%
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Put, $"{Api}/{id}", cashier, new { externalOrderCode = "X1", discountPercent = 10m, discountAmount = (decimal?)null })).StatusCode);
        var updated = await OkAsync(_admin.SendAsync(HttpMethod.Put, $"{Api}/{id}", manager, new { externalOrderCode = "X-" + Code(), discountPercent = 10m, discountAmount = (decimal?)null }));
        Assert.Equal((10_000m, 90_000m, 63_450m), (updated.GetProperty("discount").GetDecimal(), updated.GetProperty("netRevenue").GetDecimal(), updated.GetProperty("receivedAmount").GetDecimal()));
    }

    [Fact]
    public async Task Huy_can_ly_do_va_khong_bao_hoan_tien()
    {
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var id = (await OkAsync(_admin.SendAsync(HttpMethod.Post, Api, cashier, Body(menu, "tensho", "shopee", Code())))).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, $"{Api}/{id}/cancel", manager, new { reason = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Post, $"{Api}/{id}/cancel", manager, new { reason = "Khách hủy trên app" })).StatusCode);

        // Đơn app hoàn tất ngay khi nhập nên hủy là xóa bill: giữ dòng, có lý do, không theo dõi hoàn tiền
        await using var db = _fixture.CreateContext();
        var saved = await db.Orders.SingleAsync(o => o.Id == id);
        Assert.Equal(OrderStatus.Cancelled, saved.Status);
        Assert.NotNull(saved.DeletedAtUtc);
        Assert.Equal("Khách hủy trên app", saved.CancelReason);
        Assert.False(saved.RefundPending);
    }
}
