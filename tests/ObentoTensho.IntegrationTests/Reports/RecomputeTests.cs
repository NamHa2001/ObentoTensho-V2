using System.Net;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Reports.Domain;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;

namespace ObentoTensho.IntegrationTests.Reports;

// PERF-55: nhập dữ liệu cũ thì tính lại DailyStats cho khoảng ngày
[Collection(SqlServerFixture.CollectionName)]
public sealed class RecomputeTests : IDisposable
{
    private const string Path = "/api/v1/admin/reports/recompute";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public RecomputeTests(SqlServerFixture fixture)
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

    private static object Range(DateOnly from, DateOnly to) => new { fromDate = OrderApiClient.Date(from), toDate = OrderApiClient.Date(to) };

    [Fact]
    public async Task Tinh_lai_ngay_cu_tu_bang_goc_co_gioi_han_va_phan_quyen()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var email = AuthApiClient.NewEmail();
        Assert.Equal(HttpStatusCode.Created, (await new OrderApiClient(_http).CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Rice, 1, menu.Small)], email: email))).StatusCode);

        // Đơn "nhập từ dữ liệu cũ": tạo và thanh toán 200 ngày trước (12:00 giờ VN)
        var day = TestSchedule.TodayVn.AddDays(-200);
        var at = day.ToDateTime(new TimeOnly(5, 0), DateTimeKind.Utc);
        await using (var db = _fixture.CreateContext())
        {
            await db.Orders.Where(o => o.CustomerEmail == email).ExecuteUpdateAsync(s => s
                .SetProperty(o => o.CreatedAtUtc, at)
                .SetProperty(o => o.PaidAtUtc, at)
                .SetProperty(o => o.PaymentStatus, PaymentStatus.Paid));
            await db.DailyStats.Where(s => s.Date == day).ExecuteDeleteAsync();
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Post, Path, cashier, Range(day, day))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, Path, manager, Range(day, day.AddDays(92)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, Path, manager, Range(day.AddDays(1), day))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, Path, manager, Range(TestSchedule.TodayVn, TestSchedule.TodayVn.AddDays(1)))).StatusCode);

        var result = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, Path, manager, Range(day.AddDays(-2), day)));
        Assert.Equal(3, result.GetProperty("days").GetInt32());

        await using var check = _fixture.CreateContext();
        Assert.Equal(1, await check.DailyStats.Where(s => s.Date == day && s.Metric == StatMetrics.OrdersCreated && s.Dimension == string.Empty).Select(s => s.Value).SingleAsync());
        Assert.True(await check.DailyStats.AnyAsync(s => s.Date == day && s.Metric == StatMetrics.RevenuePaid && s.Value > 0));
        Assert.True(await check.AuditLogs.AnyAsync(a => a.Action == "reports.daily_stats.recomputed"));
    }
}
