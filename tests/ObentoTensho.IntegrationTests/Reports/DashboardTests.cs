using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Reports.Abstractions;
using ObentoTensho.Core.Reports.Domain;
using ObentoTensho.Core.Reports.Services;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;

namespace ObentoTensho.IntegrationTests.Reports;

// docs/srs/19 §1, §5: số liệu theo ngày VN, định nghĩa doanh thu, đối soát, cắt trường theo vai trò
[Collection(SqlServerFixture.CollectionName)]
public sealed class DashboardTests : IDisposable
{
    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;
    private readonly OrderApiClient _orders;

    public DashboardTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _admin = new AdminAuthApiClient(_factory, fixture);
        _orders = new OrderApiClient(_http);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    // Đơn thật qua API rồi đặt mốc thời gian vào một năm tương lai riêng cho từng test
    private async Task<Guid> OrderAsync(OrderTestMenu menu)
    {
        var email = AuthApiClient.NewEmail();
        Assert.Equal(HttpStatusCode.Created, (await _orders.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Rice, 2, menu.Large)], email: email))).StatusCode);
        await using var db = _fixture.CreateContext();
        return await db.Orders.Where(o => o.CustomerEmail == email).Select(o => o.Id).SingleAsync();
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    // Doanh thu tính theo ngày hoàn thành (chủ quán 2026-11-26): completed là lúc đơn hoàn tất (đã thu tiền); cancelled là lúc bill bị hủy/xóa
    private async Task SetTimesAsync(Guid id, DateTime created, DateTime? completed, DateTime? cancelled = null)
    {
        var status = cancelled is not null ? OrderStatus.Cancelled : completed is not null ? OrderStatus.Completed : OrderStatus.Preparing;
        await using var db = _fixture.CreateContext();
        await db.Orders.Where(o => o.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(o => o.CreatedAtUtc, created)
            .SetProperty(o => o.PaidAtUtc, completed)
            .SetProperty(o => o.PaymentStatus, completed is null ? PaymentStatus.Unpaid : PaymentStatus.Paid)
            .SetProperty(o => o.CompletedAtUtc, completed)
            .SetProperty(o => o.CancelledAtUtc, cancelled)
            .SetProperty(o => o.Status, status));
    }

    private async Task<Dictionary<(string Metric, string Dimension), decimal>> RollupAsync(DateOnly day)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<StatsRollupService>().RefreshDayAsync(day, CancellationToken.None);
        var rows = await scope.ServiceProvider.GetRequiredService<IStatsRepository>().ReadAsync(day, day, CancellationToken.None);
        return rows.ToDictionary(r => (r.Metric, r.Dimension), r => r.Value);
    }

    [Fact]
    public async Task Don_18h_UTC_31_12_thuoc_ngay_01_01_gio_VN()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var id = await OrderAsync(menu);
        await SetTimesAsync(id, Utc(2041, 12, 31, 18), Utc(2041, 12, 31, 18));

        var newYear = await RollupAsync(new DateOnly(2042, 1, 1));
        var eve = await RollupAsync(new DateOnly(2041, 12, 31));

        Assert.Equal(1, newYear[(StatMetrics.OrdersCreated, string.Empty)]);
        Assert.True(newYear[(StatMetrics.RevenuePaid, string.Empty)] > 0);
        Assert.Equal(1, newYear[(StatMetrics.OrdersCreated, "hour:1")]);
        Assert.Empty(eve);
    }

    [Fact]
    public async Task Doanh_thu_theo_ngay_hoan_thanh_don_dat_theo_ngay_tao_bill_xoa_thi_mat_khoi_ngay_ban()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var id = await OrderAsync(menu);

        // 23:30 VN ngày 30 = 16:30 UTC; hoàn tất 09:00 VN ngày 31
        await SetTimesAsync(id, Utc(2043, 3, 30, 16, 30), Utc(2043, 3, 31, 2));
        var created = await RollupAsync(new DateOnly(2043, 3, 30));
        var paid = await RollupAsync(new DateOnly(2043, 3, 31));
        Assert.Equal(1, created[(StatMetrics.OrdersCreated, string.Empty)]);
        Assert.False(created.ContainsKey((StatMetrics.RevenuePaid, string.Empty)));
        var amount = paid[(StatMetrics.RevenuePaid, string.Empty)];
        Assert.False(paid.ContainsKey((StatMetrics.OrdersCreated, string.Empty)));
        Assert.Equal(amount, paid.Single(p => string.Equals(p.Key.Metric, StatMetrics.RevenuePaid, StringComparison.Ordinal) && p.Key.Dimension.StartsWith(StatMetrics.CategoryPrefix, StringComparison.Ordinal)).Value);

        // Bill bị xóa ngày 02/04: doanh thu ngày bán (31) mất khoản đó, không có khoản trừ hoàn tiền ở ngày xóa
        await SetTimesAsync(id, Utc(2043, 3, 30, 16, 30), Utc(2043, 3, 31, 2), Utc(2043, 4, 2, 3));
        var after = await RollupAsync(new DateOnly(2043, 3, 31));
        var deleted = await RollupAsync(new DateOnly(2043, 4, 2));
        Assert.False(after.ContainsKey((StatMetrics.RevenuePaid, string.Empty)));
        Assert.False(deleted.ContainsKey((StatMetrics.RefundsAmount, string.Empty)));
        Assert.Equal(1, deleted[(StatMetrics.OrdersCancelled, string.Empty)]);
    }

    [Fact]
    public async Task Doi_soat_phat_hien_lech_tinh_lai_va_bao_SuperAdmin()
    {
        await _admin.SignInAsync(StaffRole.SuperAdmin);
        await using var scope = _factory.Services.CreateAsyncScope();
        var rollup = scope.ServiceProvider.GetRequiredService<StatsRollupService>();
        await rollup.ReconcileAsync(CancellationToken.None);
        Assert.Equal(0, await rollup.ReconcileAsync(CancellationToken.None));

        var day = TestSchedule.TodayVn.AddDays(-3);
        await using (var db = _fixture.CreateContext())
        {
            db.DailyStats.Add(new DailyStat { Date = day, Metric = StatMetrics.RevenuePaid, Dimension = string.Empty, Value = 999, UpdatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        Assert.Equal(1, await rollup.ReconcileAsync(CancellationToken.None));
        await using var check = _fixture.CreateContext();
        Assert.False(await check.DailyStats.AnyAsync(s => s.Date == day && s.Value == 999));
        Assert.True(await check.OutboxMessages.AnyAsync(m => m.IdempotencyKey.StartsWith("admin_alert:stats:")));
    }

    [Fact]
    public async Task Le_tan_khong_nhan_truong_doanh_thu_va_quan_ly_co_du()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var id = await OrderAsync(menu);
        await SetTimesAsync(id, DateTime.UtcNow, DateTime.UtcNow);
        var reception = await _admin.SignInAsync(StaffRole.Reception);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var kitchen = await _admin.SignInAsync(StaffRole.Kitchen);

        var limited = await _admin.SendAsync(HttpMethod.Get, "/api/v1/admin/dashboard", reception);
        var raw = await limited.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, limited.StatusCode);
        Assert.DoesNotContain("revenue", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reservationsToday", raw, StringComparison.Ordinal);

        var full = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, "/api/v1/admin/dashboard", manager));

        // Các test khác trong cùng ngày cũng có đơn: đối chiếu với bảng gốc thay vì một con số cố định
        var start = TestSchedule.TodayVn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
        var end = start.AddDays(1);
        await using var db = _fixture.CreateContext();
        var completed = await db.Orders.Where(o => o.Status == OrderStatus.Completed && o.CompletedAtUtc >= start && o.CompletedAtUtc < end).SumAsync(o => o.TotalAmount);
        Assert.True(completed > 0);
        Assert.Equal(completed, full.GetProperty("revenueToday").GetProperty("current").GetDecimal());
        Assert.Equal(7, full.GetProperty("revenueByDay").GetArrayLength());
        Assert.True(full.GetProperty("topItems").GetArrayLength() > 0);
        Assert.NotEmpty(full.GetProperty("topItems")[0].GetProperty("name").GetString()!);

        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, "/api/v1/admin/dashboard", kitchen)).StatusCode);
    }
}
