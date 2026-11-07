using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Reports.Domain;
using ObentoTensho.Core.Reports.Services;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;
using ObentoTensho.IntegrationTests.Reservations;

namespace ObentoTensho.IntegrationTests.Reports;

// docs/srs/19 §2, §5: báo cáo dùng chung nguồn với bảng tin, tổng khớp chi tiết, giới hạn khoảng ngày
[Collection(SqlServerFixture.CollectionName)]
public sealed class ReportTests : IDisposable
{
    private const string Reports = "/api/v1/admin/reports";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public ReportTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _admin = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    private async Task<Guid> PaidOrderAsync(OrderTestMenu menu, DateTime paidUtc)
    {
        var email = AuthApiClient.NewEmail();
        var orders = new OrderApiClient(_http);
        Assert.Equal(HttpStatusCode.Created, (await orders.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Rice, 1, menu.Small)], email: email))).StatusCode);
        await using var db = _fixture.CreateContext();
        var id = await db.Orders.Where(o => o.CustomerEmail == email).Select(o => o.Id).SingleAsync();
        await db.Orders.Where(o => o.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(o => o.CreatedAtUtc, paidUtc)
            .SetProperty(o => o.PaidAtUtc, paidUtc)
            .SetProperty(o => o.PaymentStatus, PaymentStatus.Paid)
            .SetProperty(o => o.Status, OrderStatus.Preparing));
        return id;
    }

    private async Task RollupAsync(params DateOnly[] days)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var rollup = scope.ServiceProvider.GetRequiredService<StatsRollupService>();
        foreach (var day in days)
        {
            await rollup.RefreshDayAsync(day, CancellationToken.None);
        }
    }

    private async Task<JsonElement> GetAsync(string token, string path) =>
        await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Reports}/{path}", token));

    [Fact]
    public async Task Doanh_thu_tong_khop_chi_tiet_nhom_theo_thang_va_so_ky_truoc()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);

        // Giữa ngày VN, ở một năm không test nào khác dùng
        await PaidOrderAsync(menu, new DateTime(2051, 1, 15, 5, 0, 0, DateTimeKind.Utc));
        await PaidOrderAsync(menu, new DateTime(2051, 2, 10, 5, 0, 0, DateTimeKind.Utc));
        await PaidOrderAsync(menu, new DateTime(2050, 12, 5, 5, 0, 0, DateTimeKind.Utc));
        await RollupAsync(new DateOnly(2051, 1, 15), new DateOnly(2051, 2, 10), new DateOnly(2050, 12, 5));

        var report = await GetAsync(manager, "revenue?from=2051-01-01&to=2051-02-28&groupBy=month");

        var series = report.GetProperty("series").EnumerateArray().ToArray();
        Assert.Equal(["2051-01", "2051-02"], series.Select(p => p.GetProperty("period").GetString()!), StringComparer.Ordinal);
        Assert.Equal(report.GetProperty("net").GetDecimal(), series.Sum(p => p.GetProperty("net").GetDecimal()));
        Assert.True(report.GetProperty("net").GetDecimal() > 0);
        Assert.Equal(report.GetProperty("net").GetDecimal() / 2, report.GetProperty("previousNet").GetDecimal());
        Assert.Equal(report.GetProperty("paid").GetDecimal(), report.GetProperty("byCategory").EnumerateArray().Sum(c => c.GetProperty("value").GetDecimal()));
        Assert.NotEmpty(report.GetProperty("byCategory")[0].GetProperty("name").GetString()!);
        Assert.Contains("ngày thanh toán", report.GetProperty("definition").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Khoang_ngay_sai_va_quyen_bi_chan()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var reception = await _admin.SignInAsync(StaffRole.Reception);

        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, $"{Reports}/revenue?from=2026-01-01&to=2026-01-31", reception)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Get, $"{Reports}/revenue?from=2026-02-01&to=2026-01-31", manager)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Get, $"{Reports}/orders?from=2025-01-01&to=2026-01-02", manager)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Get, $"{Reports}/orders?from=2025-01-01&to=2026-01-01", manager)).StatusCode);
    }

    [Fact]
    public async Task Bao_cao_dat_ban_don_hang_va_mon_ban_chay()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var day = new DateOnly(2053, 5, 6);
        var paid = new DateTime(2053, 5, 6, 5, 0, 0, DateTimeKind.Utc);
        await PaidOrderAsync(menu, paid);
        await PaidOrderAsync(menu, paid);

        var booking = AuthApiClient.NewEmail();
        Assert.Equal(HttpStatusCode.Created, (await new ReservationApiClient(_http).CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow, email: booking))).StatusCode);
        await using (var db = _fixture.CreateContext())
        {
            await db.Reservations.Where(r => r.GuestEmail == booking).ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ReservedDate, day)
                .SetProperty(r => r.Status, ReservationStatus.NoShow));
        }

        await RollupAsync(day);
        var range = "from=2053-05-06&to=2053-05-06";

        var reservations = await GetAsync(manager, $"reservations?{range}");
        Assert.Equal((1, 0, 1, 100m), (reservations.GetProperty("total").GetInt32(), reservations.GetProperty("arrived").GetInt32(), reservations.GetProperty("noShow").GetInt32(), reservations.GetProperty("noShowPercent").GetDecimal()));
        Assert.Equal("12:00", reservations.GetProperty("byHour")[0].GetProperty("name").GetString());

        var orders = await GetAsync(manager, $"orders?{range}");
        Assert.Equal(2, orders.GetProperty("created").GetInt32());
        Assert.True(orders.GetProperty("averageValue").GetDecimal() > 0);
        var dish = orders.GetProperty("topDishes")[0];
        Assert.Equal((menu.Rice, 2m), (dish.GetProperty("id").GetInt32(), dish.GetProperty("quantity").GetDecimal()));

        var top = await GetAsync(manager, $"top-dishes?{range}&limit=1000");
        Assert.Single(top.EnumerateArray());
        Assert.Single((await GetAsync(manager, $"top-dishes?{range}&limit=1")).EnumerateArray());
    }

    [Fact]
    public async Task Bao_cao_khach_co_retention_va_top_khach()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var auth = new AuthApiClient(_http);
        var (returning, _) = await auth.RegisterAndLoginAsync(AuthApiClient.NewEmail());
        var (once, _) = await auth.RegisterAndLoginAsync(AuthApiClient.NewEmail());
        var orders = new OrderApiClient(_http);
        var paid = new DateTime(2055, 3, 3, 5, 0, 0, DateTimeKind.Utc);
        foreach (var token in new[] { returning, returning, once })
        {
            var email = AuthApiClient.NewEmail();
            Assert.Equal(HttpStatusCode.Created, (await orders.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Rice, 1, menu.Small)], email: email), token)).StatusCode);
            await using var db = _fixture.CreateContext();
            await db.Orders.Where(o => o.CustomerEmail == email).ExecuteUpdateAsync(s => s
                .SetProperty(o => o.CreatedAtUtc, paid)
                .SetProperty(o => o.PaidAtUtc, paid)
                .SetProperty(o => o.PaymentStatus, PaymentStatus.Paid));
        }

        var report = await GetAsync(manager, "customers?from=2055-03-01&to=2055-03-31&limit=5");

        Assert.Equal(50m, report.GetProperty("retentionPercent").GetDecimal());
        Assert.Equal(2, report.GetProperty("topCustomers").GetArrayLength());
        Assert.Equal(2, report.GetProperty("topCustomers")[0].GetProperty("orders").GetInt32());
        Assert.Contains("90 ngày", report.GetProperty("retentionDefinition").GetString()!, StringComparison.Ordinal);
        Assert.NotEmpty(report.GetProperty("tiers").EnumerateArray());
    }

    [Fact]
    public async Task Xuat_Excel_mo_duoc_va_co_audit()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        await PaidOrderAsync(menu, new DateTime(2057, 7, 7, 5, 0, 0, DateTimeKind.Utc));
        await RollupAsync(new DateOnly(2057, 7, 7));

        var file = await _admin.SendAsync(HttpMethod.Get, $"{Reports}/revenue/export?from=2057-07-01&to=2057-07-31", manager);

        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        using (var book = new XLWorkbook(await file.Content.ReadAsStreamAsync()))
        {
            Assert.Equal("Doanh thu", book.Worksheet(1).Cell(1, 4).GetString());
            Assert.Equal("2057-07-07", book.Worksheet(1).Cell(2, 1).GetString());
        }

        Assert.Equal(HttpStatusCode.NotFound, (await _admin.SendAsync(HttpMethod.Get, $"{Reports}/hack/export?from=2057-07-01&to=2057-07-31", manager)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Get, $"{Reports}/revenue/export?from=2057-07-01&to=2057-07-31&format=csv", manager)).StatusCode);
        foreach (var report in new[] { "reservations", "orders", "customers", "top-dishes" })
        {
            Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Get, $"{Reports}/{report}/export?from=2057-07-01&to=2057-07-31", manager)).StatusCode);
        }

        await using var db = _fixture.CreateContext();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "reports.Report.exported" && a.EntityId == "revenue"));
    }

    [Fact]
    public async Task Bao_cao_khop_bang_tin_cho_ngay_hom_nay()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        await PaidOrderAsync(menu, DateTime.UtcNow);
        var today = TestSchedule.TodayVn.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        var report = await GetAsync(manager, $"revenue?from={today}&to={today}");
        var board = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, "/api/v1/admin/dashboard", manager));

        Assert.Equal(board.GetProperty("revenueToday").GetProperty("current").GetDecimal(), report.GetProperty("net").GetDecimal());
    }
}
