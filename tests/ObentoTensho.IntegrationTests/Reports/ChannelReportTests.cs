using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Tables.Domain;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Delivery;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;

namespace ObentoTensho.IntegrationTests.Reports;

// Doanh thu theo kênh (mang về / tại quán / giao hàng) và tiền ship thực thu; món đã hủy ở bàn không tính vào doanh thu món.
// Đơn được dời về một ngày cũ riêng rồi tính lại DailyStats, để số liệu không lẫn đơn của test khác.
[Collection(SqlServerFixture.CollectionName)]
public sealed class ChannelReportTests : IDisposable
{
    private const string Admin = "/api/v1/admin";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly OrderApiClient _orders;
    private readonly AdminAuthApiClient _admin;

    public ChannelReportTests(SqlServerFixture fixture)
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

    private static async Task<JsonElement> OkAsync(Task<HttpResponseMessage> response)
    {
        var message = await response;
        Assert.True(message.IsSuccessStatusCode, await message.Content.ReadAsStringAsync());
        return await AdminAuthApiClient.ReadDataAsync(message);
    }

    private async Task<Guid> PaidOnlineAsync(string cashier, object[] items, string? address)
    {
        var order = JsonSerializer.SerializeToElement(OrderApiClient.Order(items));
        var body = order.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value, StringComparer.Ordinal);
        if (address is not null)
        {
            body["channel"] = "delivery";
            body["deliveryAddress"] = address;
        }

        var created = await _orders.CreateAsync(body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await OrderApiClient.DataOf(created)).GetProperty("order").GetProperty("id").GetGuid();
        await OkAsync(_admin.SendAsync(HttpMethod.Patch, $"{Admin}/orders/{id}/payment", cashier));
        return id;
    }

    private async Task<Guid> PaidDineInAsync(string manager, OrderTestMenu menu)
    {
        int tableId;
        await using (var db = _fixture.CreateContext())
        {
            var area = new TableArea { Name = $"BC {Guid.NewGuid():N}"[..20] };
            db.TableAreas.Add(area);
            await db.SaveChangesAsync();
            var table = new DiningTable { AreaId = area.Id, Name = "BC1", MinCapacity = 1, MaxCapacity = 4 };
            db.Tables.Add(table);
            await db.SaveChangesAsync();
            tableId = table.Id;
        }

        var id = (await OkAsync(_admin.SendAsync(HttpMethod.Post, $"{Admin}/dine-in", manager, new { tableId, guestCount = 2 }))).GetProperty("id").GetGuid();
        var bill = await OkAsync(_admin.SendAsync(HttpMethod.Post, $"{Admin}/dine-in/{id}/items", manager, new { items = new[] { OrderApiClient.Line(menu.Tea, 2), OrderApiClient.Line(menu.DineInOnly, 1) } }));
        var voided = bill.GetProperty("items")[1].GetProperty("id").GetInt64();
        await OkAsync(_admin.SendAsync(HttpMethod.Post, $"{Admin}/dine-in/{id}/items/{voided}/void", manager, new { reason = "Khách đổi món" }));
        await OkAsync(_admin.SendAsync(HttpMethod.Post, $"{Admin}/dine-in/{id}/checkout", manager, new { paymentMethod = "cash" }));
        return id;
    }

    [Fact]
    public async Task Doanh_thu_tach_theo_kenh_va_tien_ship_mon_huy_khong_tinh()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);

        Guid[] ids =
        [
            await PaidOnlineAsync(cashier, [OrderApiClient.Line(menu.Tea, 1)], address: null),
            await PaidOnlineAsync(cashier, [OrderApiClient.Line(menu.Tea, 4)], "88 Nguyễn Huệ 4100m Quận 1"),
            await PaidDineInAsync(manager, menu),
        ];

        var day = TestSchedule.TodayVn.AddDays(-260);
        var at = day.ToDateTime(new TimeOnly(5, 0), DateTimeKind.Utc);
        int categoryId;
        await using (var db = _fixture.CreateContext())
        {
            await db.Orders.Where(o => ids.Contains(o.Id)).ExecuteUpdateAsync(s => s.SetProperty(o => o.CreatedAtUtc, at).SetProperty(o => o.PaidAtUtc, at).SetProperty(o => o.CompletedAtUtc, at).SetProperty(o => o.Status, OrderStatus.Completed));
            await db.DailyStats.Where(s => s.Date == day).ExecuteDeleteAsync();
            categoryId = await db.MenuItems.Where(m => m.Id == menu.Tea).Select(m => m.CategoryId).SingleAsync();
        }

        var range = new { fromDate = OrderApiClient.Date(day), toDate = OrderApiClient.Date(day) };
        await OkAsync(_admin.SendAsync(HttpMethod.Post, $"{Admin}/reports/recompute", manager, range));
        var date = OrderApiClient.Date(day);
        var report = await OkAsync(_admin.SendAsync(HttpMethod.Get, $"{Admin}/reports/revenue?from={date}&to={date}&groupBy=day", manager));

        var channels = report.GetProperty("byChannel").EnumerateArray().ToDictionary(c => c.GetProperty("channel").GetString()!, StringComparer.Ordinal);
        Assert.Equal((1, 5_000m), (channels["pickup"].GetProperty("orders").GetInt32(), channels["pickup"].GetProperty("net").GetDecimal()));
        Assert.Equal((1, 10_000m), (channels["dine_in"].GetProperty("orders").GetInt32(), channels["dine_in"].GetProperty("net").GetDecimal()));
        Assert.Equal((1, 65_000m), (channels["delivery"].GetProperty("orders").GetInt32(), channels["delivery"].GetProperty("net").GetDecimal()));
        Assert.Equal(45_000, report.GetProperty("shippingCollected").GetDecimal());

        // Doanh thu danh mục chỉ có tiền món chưa hủy: 5.000 + 20.000 + 10.000 (món 90.000 đã hủy không tính)
        var category = report.GetProperty("byCategory").EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == categoryId);
        Assert.Equal(35_000, category.GetProperty("value").GetDecimal());

        var file = await _admin.SendAsync(HttpMethod.Get, $"{Admin}/reports/channels/export?from={date}&to={date}", manager);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
    }
}
