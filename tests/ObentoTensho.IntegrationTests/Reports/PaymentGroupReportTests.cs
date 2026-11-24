using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;

namespace ObentoTensho.IntegrationTests.Reports;

// docs/srs/23 §9: báo cáo theo 4 nhóm thanh toán khớp ví dụ trong SRS; đơn hủy giữ dòng chi tiết nhưng không tính;
// tổng chi tiết khớp tổng nhóm; file Excel hai sheet có ô số, file PDF mở được
[Collection(SqlServerFixture.CollectionName)]
public sealed class PaymentGroupReportTests : IDisposable
{
    private const string Admin = "/api/v1/admin";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly AdminAuthApiClient _admin;

    public PaymentGroupReportTests(SqlServerFixture fixture)
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

    private Task<HttpResponseMessage> Post(string path, string token, object? body = null) =>
        _admin.SendAsync(HttpMethod.Post, $"{Admin}{path}", token, body ?? new { });

    // Bếp ra hết món ⇒ đơn tự hoàn thành
    private async Task ServeAllAsync(string kitchen, Guid id)
    {
        var ticket = (await OkAsync(_admin.SendAsync(HttpMethod.Get, $"{Admin}/dine-in/kitchen-view", kitchen))).EnumerateArray().Single(t => t.GetProperty("billId").GetGuid() == id);
        foreach (var line in ticket.GetProperty("items").EnumerateArray())
        {
            await OkAsync(Post($"/dine-in/{id}/items/{line.GetProperty("id").GetInt64()}/served", kitchen));
        }
    }

    private static decimal Amount(JsonElement amounts, string name) => amounts.GetProperty(name).GetDecimal();

    [Fact]
    public async Task Tong_theo_nhom_khop_vi_du_srs_don_huy_khong_tinh_va_xuat_file_khop_so()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var (day, cancelled) = await SeedAsync(manager);
        var date = OrderApiClient.Date(day);

        var total = await AssertSummaryAsync(manager, date);
        await AssertDetailAsync(manager, date, cancelled, total);
        await AssertExportsAsync(manager, date);
    }

    // Grab Tensho 100.000 giảm 15%, tại quán tiền mặt 200.000, một đơn Be Hima đã hủy; dời sang một ngày riêng để không lẫn test khác
    private async Task<(DateOnly Day, Guid Cancelled)> SeedAsync(string manager)
    {
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var kitchen = await _admin.SignInAsync(StaffRole.Kitchen);

        // Grab tài khoản Tensho: giá gốc 100.000, giảm 15%
        var grab = (await OkAsync(Post("/platform-orders", cashier, new
        {
            paymentGroup = "tensho",
            platform = "grab",
            externalOrderCode = "RPT-" + Guid.NewGuid().ToString("N")[..8],
            discountPercent = 15m,
            items = new[] { OrderApiClient.Line(menu.Tea, 2), OrderApiClient.Line(menu.DineInOnly, 1) },
        }))).GetProperty("id").GetGuid();
        await ServeAllAsync(kitchen, grab);

        // Tại quán tiền mặt 200.000, không giảm
        var counter = (await OkAsync(Post("/dine-in/counter", cashier, new { customerName = "Khách lẻ" }))).GetProperty("id").GetGuid();
        await OkAsync(Post($"/dine-in/{counter}/items", cashier, new { items = new[] { OrderApiClient.Line(menu.DineInOnly, 2), OrderApiClient.Line(menu.Tea, 4) } }));
        await OkAsync(Post($"/dine-in/{counter}/checkout", cashier, new { paymentMethod = "cash" }));
        await ServeAllAsync(kitchen, counter);

        // Đơn app hủy: hiện ở chi tiết, không tính
        var cancelled = (await OkAsync(Post("/platform-orders", cashier, new
        {
            paymentGroup = "hima",
            platform = "be",
            externalOrderCode = "RPT-" + Guid.NewGuid().ToString("N")[..8],
            items = new[] { OrderApiClient.Line(menu.Tea, 1) },
        }))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await Post($"/platform-orders/{cancelled}/cancel", manager, new { reason = "Khách hủy trên app" })).StatusCode);

        // Dời sang một ngày riêng để không lẫn đơn của test khác
        var day = TestSchedule.TodayVn.AddDays(-330);
        var at = day.ToDateTime(new TimeOnly(5, 0), DateTimeKind.Utc);
        await using (var db = _fixture.CreateContext())
        {
            Guid[] ids = [grab, counter];
            await db.Orders.Where(o => ids.Contains(o.Id)).ExecuteUpdateAsync(s => s.SetProperty(o => o.CompletedAtUtc, at));
            await db.Orders.Where(o => o.Id == cancelled).ExecuteUpdateAsync(s => s.SetProperty(o => o.CancelledAtUtc, at));
        }

        return (day, cancelled);
    }

    private async Task<JsonElement> AssertSummaryAsync(string manager, string date)
    {
        var report = await OkAsync(_admin.SendAsync(HttpMethod.Get, $"{Admin}/reports/payment-groups?from={date}&to={date}&groupBy=day", manager));
        var groups = report.GetProperty("groups").EnumerateArray().ToDictionary(g => g.GetProperty("group").GetString()!, StringComparer.Ordinal);
        Assert.Equal(["in_store", "tensho", "hima", "xanh"], groups.Keys, StringComparer.Ordinal);

        var tensho = groups["tensho"].GetProperty("amounts");
        Assert.Equal((1, 100_000m, 15_000m, 85_000m), (tensho.GetProperty("orders").GetInt32(), Amount(tensho, "subTotal"), Amount(tensho, "discount"), Amount(tensho, "netRevenue")));
        Assert.Equal((21_250m, 2_550m, 1_275m, 59_925m), (Amount(tensho, "deductionAmount"), Amount(tensho, "vatAmount"), Amount(tensho, "pitAmount"), Amount(tensho, "receivedAmount")));
        var grabRow = groups["tensho"].GetProperty("channels").EnumerateArray().Single(c => string.Equals(c.GetProperty("platform").GetString(), "grab", StringComparison.Ordinal));
        Assert.Equal(59_925m, Amount(grabRow.GetProperty("amounts"), "receivedAmount"));

        var inStore = groups["in_store"];
        Assert.Equal((200_000m, 141_000m), (Amount(inStore.GetProperty("amounts"), "netRevenue"), Amount(inStore.GetProperty("amounts"), "receivedAmount")));
        var cash = inStore.GetProperty("channels").EnumerateArray().Single(c => string.Equals(c.GetProperty("paymentMethod").GetString(), "cash", StringComparison.Ordinal));
        Assert.Equal(1, cash.GetProperty("amounts").GetProperty("orders").GetInt32());

        Assert.Equal(0, groups["hima"].GetProperty("amounts").GetProperty("orders").GetInt32());
        var total = report.GetProperty("total");
        Assert.Equal((2, 285_000m, 200_925m), (total.GetProperty("orders").GetInt32(), Amount(total, "netRevenue"), Amount(total, "receivedAmount")));
        return total.Clone();
    }

    private async Task AssertDetailAsync(string manager, string date, Guid cancelled, JsonElement total)
    {

        // Chi tiết: 3 dòng (gồm đơn hủy), tổng đơn hoàn thành khớp tổng nhóm
        var page = await _admin.SendAsync(HttpMethod.Get, $"{Admin}/reports/payment-groups/orders?from={date}&to={date}&page=1&pageSize=20", manager);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        using var paged = JsonDocument.Parse(await page.Content.ReadAsStringAsync());
        var rows = paged.RootElement.GetProperty("data").EnumerateArray().ToArray();
        Assert.Equal(3, paged.RootElement.GetProperty("meta").GetProperty("total").GetInt32());
        var cancelledRow = rows.Single(r => r.GetProperty("id").GetGuid() == cancelled);
        Assert.Equal(("cancelled", 0m), (cancelledRow.GetProperty("status").GetString(), cancelledRow.GetProperty("receivedAmount").GetDecimal()));
        Assert.Equal(Amount(total, "receivedAmount"), rows.Sum(r => r.GetProperty("receivedAmount").GetDecimal()));
    }

    private async Task AssertExportsAsync(string manager, string date)
    {

        // Excel: sheet tổng và sheet chi tiết, ô tiền là số
        var xlsx = await _admin.SendAsync(HttpMethod.Get, $"{Admin}/reports/payment-groups/export?from={date}&to={date}&format=xlsx", manager);
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        using (var book = new XLWorkbook(new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync())))
        {
            Assert.Equal(["Tổng theo nhóm", "Chi tiết từng đơn"], book.Worksheets.Select(w => w.Name), StringComparer.Ordinal);
            var summary = book.Worksheet(1);
            var totalRow = summary.RowsUsed().Last();
            Assert.Equal("Tổng cộng", totalRow.Cell(1).GetString());
            Assert.True(totalRow.Cell(10).DataType == XLDataType.Number);
            Assert.Equal(200_925m, (decimal)totalRow.Cell(10).GetDouble());
            Assert.Equal(3, book.Worksheet(2).RowsUsed().Count(r => r.Cell(2).GetString().Length > 0 && r.Cell(1).DataType == XLDataType.DateTime));
        }

        var pdf = await _admin.SendAsync(HttpMethod.Get, $"{Admin}/reports/payment-groups/export?from={date}&to={date}&format=pdf", manager);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync())[..4]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Thu_ngan_khong_xem_duoc_bao_cao()
    {
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var date = OrderApiClient.Date(TestSchedule.TodayVn);

        var response = await _admin.SendAsync(HttpMethod.Get, $"{Admin}/reports/payment-groups?from={date}&to={date}", cashier);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
