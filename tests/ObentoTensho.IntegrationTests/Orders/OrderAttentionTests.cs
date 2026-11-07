using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Orders.Services;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Orders;

// BR-ORD-18 (không đến lấy, cờ theo SĐT) và docs/srs/20 #30 (danh sách + thư "cần xử lý" cho quản lý)
[Collection(SqlServerFixture.CollectionName)]
public sealed class OrderAttentionTests : IDisposable
{
    private const string Admin = "/api/v1/admin/orders";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly OrderApiClient _orders;
    private readonly AdminAuthApiClient _admin;

    public OrderAttentionTests(SqlServerFixture fixture)
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
    public async Task Hai_lan_khong_den_lay_thi_SDT_bi_gan_co_va_don_sau_phai_goi_xac_nhan()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var phone = OrderApiClient.NewPhone();

        // Khách đã từng nhận đơn ⇒ đơn mới không cần gọi
        await CompletedOrderAsync(menu, phone);
        var (first, _) = await CreateAsync(menu, phone);
        Assert.False((await Detail(cashier, first)).GetProperty("needsContactConfirm").GetBoolean());

        // Chỉ đơn đã sẵn sàng mới hủy "không đến lấy"
        Assert.Equal(HttpStatusCode.BadRequest, (await Status(cashier, first, "pending", "cancelled", noPickup: true)).StatusCode);

        foreach (var id in new[] { first, (await CreateAsync(menu, phone)).Id })
        {
            await SetStatusAsync(id, OrderStatus.Ready);
            Assert.Equal(HttpStatusCode.OK, (await Status(cashier, id, "ready", "cancelled", noPickup: true)).StatusCode);
        }

        var cancelled = await Detail(cashier, first);
        Assert.True(cancelled.GetProperty("noPickupFlag").GetBoolean());

        var (next, _) = await CreateAsync(menu, phone);
        var flagged = await Detail(cashier, next);
        Assert.True(flagged.GetProperty("needsContactConfirm").GetBoolean());
        Assert.True(flagged.GetProperty("noPickupFlag").GetBoolean());

        await using var db = _fixture.CreateContext();
        Assert.Equal("Khách không đến lấy", (await db.Orders.SingleAsync(o => o.Id == first)).CancelReason);
    }

    [Fact]
    public async Task Don_can_xu_ly_hien_trong_danh_sach_va_quan_ly_nhan_mot_thu_gop()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var (_, managerEmail) = await _admin.CreateStaffAsync(StaffRole.Manager);

        var (stale, staleCode) = await CreateAsync(menu, OrderApiClient.NewPhone());
        var (late, lateCode) = await CreateAsync(menu, OrderApiClient.NewPhone());
        var (fresh, _) = await CreateAsync(menu, OrderApiClient.NewPhone());
        await using (var db = _fixture.CreateContext())
        {
            await db.Orders.Where(o => o.Id == stale).ExecuteUpdateAsync(s => s.SetProperty(o => o.CreatedAtUtc, DateTime.UtcNow.AddHours(-1)));
            await db.Orders.Where(o => o.Id == late).ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Ready)
                .SetProperty(o => o.PickupAtUtc, DateTime.UtcNow.AddHours(-2)));
        }

        var attention = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/attention", cashier));
        Assert.Contains(Codes(attention, "pendingOverdue"), c => string.Equals(c, staleCode, StringComparison.Ordinal));
        Assert.Contains(Codes(attention, "notPickedUp"), c => string.Equals(c, lateCode, StringComparison.Ordinal));
        Assert.DoesNotContain(fresh.ToString(), attention.ToString(), StringComparison.Ordinal);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var jobs = scope.ServiceProvider.GetRequiredService<OrderJobs>();
            Assert.True(await jobs.NotifyAttentionAsync(CancellationToken.None) >= 2);
            Assert.Equal(0, await jobs.NotifyAttentionAsync(CancellationToken.None));
        }

        await _factory.ProcessOutboxAsync();
        var mail = Assert.Single(_factory.Emails.To(managerEmail));
        Assert.Contains(staleCode, mail.TextBody, StringComparison.Ordinal);
        Assert.Contains(lateCode, mail.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("+84", mail.TextBody, StringComparison.Ordinal);
    }

    private async Task CompletedOrderAsync(OrderTestMenu menu, string phone)
    {
        var (id, _) = await CreateAsync(menu, phone);
        await using var db = _fixture.CreateContext();
        await db.Orders.Where(o => o.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(o => o.Status, OrderStatus.Completed)
            .SetProperty(o => o.PaymentStatus, PaymentStatus.Paid));
    }

    private async Task SetStatusAsync(Guid id, OrderStatus status)
    {
        await using var db = _fixture.CreateContext();
        await db.Orders.Where(o => o.Id == id).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, status));
    }

    private async Task<(Guid Id, string Code)> CreateAsync(OrderTestMenu menu, string phone)
    {
        var created = await _orders.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Tea, 1)], phone: phone));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var code = (await OrderApiClient.DataOf(created)).GetProperty("order").GetProperty("code").GetString()!;
        await using var db = _fixture.CreateContext();
        return (await db.Orders.Where(o => o.OrderCode == code).Select(o => o.Id).SingleAsync(), code);
    }

    private Task<HttpResponseMessage> Status(string token, Guid id, string expected, string status, bool noPickup) =>
        _admin.SendAsync(HttpMethod.Patch, $"{Admin}/{id}/status", token, new { expectedStatus = expected, status, reason = (string?)null, noPickup });

    private async Task<JsonElement> Detail(string token, Guid id) =>
        (await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/{id}", token))).GetProperty("order");

    private static string?[] Codes(JsonElement attention, string group) =>
        attention.GetProperty(group).EnumerateArray().Select(o => o.GetProperty("code").GetString()).ToArray();
}
