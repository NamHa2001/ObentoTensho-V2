using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Contracts.Realtime;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Reservations.Services;
using ObentoTensho.Infrastructure.Security;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;
using ObentoTensho.IntegrationTests.Realtime;

namespace ObentoTensho.IntegrationTests.Reservations;

// BR-RSV-25 (nhắc đặt bàn chờ quá hạn), BR-RSV-09 (bàn "Đã đặt" đầu ngày), RT-05 (sự kiện bàn), BR-ORD-20 (Turnstile chỉ với khách vãng lai)
[Collection(SqlServerFixture.CollectionName)]
public sealed class ReservationOpsTests : IDisposable
{
    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public ReservationOpsTests(SqlServerFixture fixture)
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

    private async Task<int> RunAsync<T>()
        where T : IRecurringJob
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetServices<IRecurringJob>().OfType<T>().Single().RunAsync(CancellationToken.None);
    }

    // Mở cả ngày để test không phụ thuộc giờ chạy
    private async Task OpenAllDayAsync()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        await using var db = _fixture.CreateContext();
        await db.OpeningHours.ExecuteUpdateAsync(s => s.SetProperty(h => h.OpenTime, new TimeOnly(0, 0)).SetProperty(h => h.CloseTime, new TimeOnly(23, 59)));
    }

    private async Task<(Guid Id, string Code)> BookAsync()
    {
        var api = new ReservationApiClient(_http);
        var code = await ReservationApiClient.CodeOf(await api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow)));
        await using var db = _fixture.CreateContext();
        return (await db.Reservations.Where(r => r.ReservationCode == code).Select(r => r.Id).SingleAsync(), code);
    }

    [Fact]
    public async Task Dat_ban_cho_qua_han_nhac_le_tan_va_quan_ly_mot_lan()
    {
        await OpenAllDayAsync();
        await _admin.SignInAsync(StaffRole.Reception);
        var (oldId, oldCode) = await BookAsync();
        var (newId, _) = await BookAsync();
        await using (var db = _fixture.CreateContext())
        {
            await db.Reservations.Where(r => r.Id == oldId).ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedAtUtc, DateTime.UtcNow.AddMinutes(-45)));
        }

        Assert.True(await RunAsync<ReservationPendingAlertJob>() >= 1);
        Assert.Equal(0, await RunAsync<ReservationPendingAlertJob>());

        await using var check = _fixture.CreateContext();
        Assert.NotNull(await check.Reservations.Where(r => r.Id == oldId).Select(r => r.PendingAlertedAtUtc).SingleAsync());
        Assert.Null(await check.Reservations.Where(r => r.Id == newId).Select(r => r.PendingAlertedAtUtc).SingleAsync());
        var alerts = await check.OutboxMessages.Where(m => m.IdempotencyKey.StartsWith("admin_alert:reservation-pending:") && m.PayloadJson.Contains(oldCode)).ToListAsync();
        Assert.NotEmpty(alerts);
    }

    [Fact]
    public async Task Ngoai_gio_mo_cua_khong_nhac()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var (id, _) = await BookAsync();
        await using (var db = _fixture.CreateContext())
        {
            await db.Reservations.Where(r => r.Id == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedAtUtc, DateTime.UtcNow.AddMinutes(-45)));
            await db.OpeningHours.ExecuteUpdateAsync(s => s.SetProperty(h => h.IsClosed, true));
        }

        await RunAsync<ReservationPendingAlertJob>();

        await using var check = _fixture.CreateContext();
        Assert.Null(await check.Reservations.Where(r => r.Id == id).Select(r => r.PendingAlertedAtUtc).SingleAsync());
    }

    [Fact]
    public async Task Dau_ngay_ban_cua_dat_ban_da_xac_nhan_thanh_da_dat_va_phat_su_kien()
    {
        await OpenAllDayAsync();
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var area = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, "/api/v1/admin/table-areas", manager, new { name = $"Sân {Guid.NewGuid():N}", isActive = true }));
        var table = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, "/api/v1/admin/tables", manager, new
        {
            areaId = area.GetProperty("id").GetInt32(), name = "S1", minCapacity = 2, maxCapacity = 4,
            layout = new { x = 0, y = 0, width = 60, height = 60, rotation = 0 }, isActive = true,
        }));
        var tableId = table.GetProperty("id").GetInt32();
        var (id, _) = await BookAsync();
        await using (var db = _fixture.CreateContext())
        {
            await db.Reservations.Where(r => r.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, ReservationStatus.Confirmed)
                .SetProperty(r => r.ReservedDate, TestSchedule.TodayVn)
                .SetProperty(r => r.TableId, tableId));
        }

        var reception = await _admin.SignInAsync(StaffRole.Reception);
        var ticket = (await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, "/api/v1/admin/realtime/ticket", reception))).GetProperty("ticket").GetString();
        await using var hub = await HubTestClient.ConnectAsync(_factory, Api.Realtime.HubRoutes.Staff, ApiFactory.AdminOrigin, ticket);

        Assert.True(await RunAsync<ReservationTableDayStartJob>() >= 1);

        await using (var db = _fixture.CreateContext())
        {
            Assert.Equal(TableStatus.Reserved, await db.Tables.Where(t => t.Id == tableId).Select(t => t.CurrentStatus).SingleAsync());
        }

        var pushed = await hub.NextAsync(RealtimeEventNames.TableStatusChanged, p => p.GetProperty("id").GetInt32() == tableId);
        Assert.Equal("reserved", pushed.GetProperty("status").GetString());

        // Nhân viên đổi trạng thái bàn tại quầy cũng phát sự kiện
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Patch, $"/api/v1/admin/tables/{tableId}/status", manager, new { status = "cleaning" })).StatusCode);
        await hub.NextAsync(RealtimeEventNames.TableStatusChanged, p => p.GetProperty("id").GetInt32() == tableId && string.Equals(p.GetProperty("status").GetString(), "cleaning", StringComparison.Ordinal));

        // Bàn đang dọn thì không bị đầu ngày ghi đè
        await RunAsync<ReservationTableDayStartJob>();
        await using var check = _fixture.CreateContext();
        Assert.Equal(TableStatus.Cleaning, await check.Tables.Where(t => t.Id == tableId).Select(t => t.CurrentStatus).SingleAsync());
    }

    [Fact]
    public async Task Dat_mon_chi_khach_vang_lai_can_Turnstile()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        using var factory = new ApiFactory(_fixture.ConnectionString, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Turnstile:Enabled"] = "true",
            ["Turnstile:SecretKey"] = "test-secret",
            ["Turnstile:AllowedHostnames:0"] = "www.obento.test",
        });
        using var http = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<ITurnstileVerifier, FakeTurnstileVerifier>()))
            .CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var orders = new OrderApiClient(http);
        var order = OrderApiClient.Order([OrderApiClient.Line(menu.Rice, 1, menu.Small)]);

        Assert.Equal(ErrorCodes.CaptchaFailed, await AuthApiClient.ReadErrorCodeAsync(await orders.CreateAsync(order)));

        // Đăng ký qua máy chủ không bật Turnstile, đăng nhập ở máy chủ đang bật
        var email = AuthApiClient.NewEmail();
        (await new AuthApiClient(_http).RegisterAsync(email)).EnsureSuccessStatusCode();
        var customer = await AuthApiClient.ReadAccessTokenAsync(await new AuthApiClient(http).LoginAsync(email));
        Assert.Equal(HttpStatusCode.Created, (await orders.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Rice, 1, menu.Small)]), customer)).StatusCode);

        // Token sai thì vẫn coi là khách vãng lai
        Assert.Equal(ErrorCodes.CaptchaFailed, await AuthApiClient.ReadErrorCodeAsync(await orders.CreateAsync(order, "token-gia")));
    }
}
