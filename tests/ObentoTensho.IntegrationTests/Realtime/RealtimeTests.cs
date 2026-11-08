using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Api.Realtime;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Contracts.Realtime;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Realtime.Abstractions;
using ObentoTensho.Core.Realtime.Domain;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;
using ObentoTensho.IntegrationTests.Reservations;

namespace ObentoTensho.IntegrationTests.Realtime;

// docs/srs/06 §B.3, SEC-04, SEC-69: hub nhân viên cần vé, nhóm do server gán, khách chỉ nhận sự kiện của mình, payload không có PII
[Collection(SqlServerFixture.CollectionName)]
public sealed class RealtimeTests : IAsyncDisposable
{
    private const string AdminTicket = "/api/v1/admin/realtime/ticket";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;
    private readonly OrderApiClient _orders;
    private readonly List<HubTestClient> _clients = [];

    public RealtimeTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _admin = new AdminAuthApiClient(_factory, fixture);
        _orders = new OrderApiClient(_http);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            await client.DisposeAsync();
        }

        _http.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task<string> TicketAsync(string accessToken)
    {
        var response = await _admin.SendAsync(HttpMethod.Post, AdminTicket, accessToken);
        return (await AdminAuthApiClient.ReadDataAsync(response)).GetProperty("ticket").GetString()!;
    }

    private async Task<string> CustomerTicketAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/realtime-ticket");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return (await AdminAuthApiClient.ReadDataAsync(await _http.SendAsync(request))).GetProperty("ticket").GetString()!;
    }

    private async Task<HubTestClient> StaffAsync(StaffRole role) => await StaffAsync(await _admin.SignInAsync(role), track: true);

    private async Task<HubTestClient> StaffAsync(string accessToken, bool track)
    {
        var client = await HubTestClient.ConnectAsync(_factory, HubRoutes.Staff, ApiFactory.AdminOrigin, await TicketAsync(accessToken));
        if (track)
        {
            _clients.Add(client);
        }

        return client;
    }

    private async Task<HubTestClient> VisitorAsync(string? ticket = null)
    {
        var client = await HubTestClient.ConnectAsync(_factory, HubRoutes.Public, ApiFactory.CustomerOrigin, ticket);
        _clients.Add(client);
        return client;
    }

    private static Func<JsonElement, bool> Code(string code) =>
        p => string.Equals(p.GetProperty("code").GetString(), code, StringComparison.Ordinal);

    private async Task<(Guid Id, string Code, string Token)> OrderAsync(OrderTestMenu menu, string? customerToken = null)
    {
        var created = await _orders.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Rice, 1, menu.Small)]), customerToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var data = await OrderApiClient.DataOf(created);
        var code = data.GetProperty("order").GetProperty("code").GetString()!;
        await using var db = _fixture.CreateContext();
        var id = await db.Orders.Where(o => o.OrderCode == code).Select(o => o.Id).SingleAsync();
        return (id, code, data.GetProperty("accessToken").GetString()!);
    }

    private Task<HttpResponseMessage> CancelByStaffAsync(string token, Guid id) =>
        _admin.SendAsync(HttpMethod.Patch, $"/api/v1/admin/orders/{id}/status", token, new { expectedStatus = "pending", status = "cancelled", reason = "Hết món" });

    private static async Task<bool> RejectedAsync(HubTestClient client)
    {
        try
        {
            await client.Connection.StartAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Net.WebSockets.WebSocketException or InvalidOperationException)
        {
            return true;
        }

        // Kết nối mở được nhưng server đóng ngay
        return await client.ClosedWithinAsync();
    }

    [Fact]
    public async Task Hub_nhan_vien_tu_choi_an_danh_ve_sai_ve_da_dung_va_ve_cua_khach()
    {
        var kitchen = await _admin.SignInAsync(StaffRole.Kitchen);
        var ticket = await TicketAsync(kitchen);

        await using (var anonymous = HubTestClient.Create(_factory, HubRoutes.Staff, ApiFactory.AdminOrigin))
        {
            Assert.True(await RejectedAsync(anonymous));
        }

        await using (var forged = HubTestClient.Create(_factory, HubRoutes.Staff, ApiFactory.AdminOrigin, "ve-gia"))
        {
            Assert.True(await RejectedAsync(forged));
        }

        // Đối chứng: vé thật vào được; dùng lại vé đó lần hai bị từ chối
        await using (var first = await HubTestClient.ConnectAsync(_factory, HubRoutes.Staff, ApiFactory.AdminOrigin, ticket))
        {
            Assert.Equal(HubConnectionState.Connected, first.Connection.State);
            await using var replay = HubTestClient.Create(_factory, HubRoutes.Staff, ApiFactory.AdminOrigin, ticket);
            Assert.True(await RejectedAsync(replay));
        }

        var (customer, _) = await new AuthApiClient(_http).RegisterAndLoginAsync(AuthApiClient.NewEmail());
        await using (var asCustomer = HubTestClient.Create(_factory, HubRoutes.Staff, ApiFactory.AdminOrigin, await CustomerTicketAsync(customer)))
        {
            Assert.True(await RejectedAsync(asCustomer));
        }

        // Không xin vé được nếu chưa đăng nhập
        Assert.Equal(HttpStatusCode.Unauthorized, (await _http.PostAsync(AdminTicket, null)).StatusCode);
    }

    [Fact]
    public async Task Hub_chi_nhan_dung_Origin()
    {
        var kitchen = await _admin.SignInAsync(StaffRole.Kitchen);

        await using (var wrong = HubTestClient.Create(_factory, HubRoutes.Staff, ApiFactory.CustomerOrigin, await TicketAsync(kitchen)))
        {
            Assert.True(await RejectedAsync(wrong));
        }

        await using (var evil = HubTestClient.Create(_factory, HubRoutes.Public, "https://evil.test"))
        {
            Assert.True(await RejectedAsync(evil));
        }

        await using var ok = await HubTestClient.ConnectAsync(_factory, HubRoutes.Public, ApiFactory.CustomerOrigin);
        Assert.Equal(HubConnectionState.Connected, ok.Connection.State);
    }

    [Fact]
    public async Task Hub_cong_khai_co_ve_thi_ve_phai_dung_va_khong_nhan_ve_nhan_vien()
    {
        await using (var forged = HubTestClient.Create(_factory, HubRoutes.Public, ApiFactory.CustomerOrigin, "ve-gia"))
        {
            Assert.True(await RejectedAsync(forged));
        }

        var kitchen = await _admin.SignInAsync(StaffRole.Kitchen);
        await using (var staff = HubTestClient.Create(_factory, HubRoutes.Public, ApiFactory.CustomerOrigin, await TicketAsync(kitchen)))
        {
            Assert.True(await RejectedAsync(staff));
        }

        var (customer, _) = await new AuthApiClient(_http).RegisterAndLoginAsync(AuthApiClient.NewEmail());
        await using var member = await HubTestClient.ConnectAsync(_factory, HubRoutes.Public, ApiFactory.CustomerOrigin, await CustomerTicketAsync(customer));
        Assert.Equal(HubConnectionState.Connected, member.Connection.State);
    }

    // PERF-63: đo số kết nối đang mở và độ trễ từ hàng chờ tới khi gửi, gắn nhãn theo tên sự kiện
    [Fact]
    public async Task Do_so_ket_noi_va_do_tre_gui_su_kien()
    {
        var scope = _factory.Services.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>();
        var connections = new List<long>();
        var delays = new List<(double Ms, string? Event)>();
        var sent = new List<string?>();
        using var listener = new System.Diagnostics.Metrics.MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (string.Equals(instrument.Meter.Name, RealtimeMetrics.MeterName, StringComparison.Ordinal) && ReferenceEquals(instrument.Meter.Scope, scope))
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        static string? EventOf(ReadOnlySpan<KeyValuePair<string, object?>> tags) => tags.Length > 0 ? tags[0].Value as string : null;
        listener.SetMeasurementEventCallback<int>((instrument, value, _, _) =>
        {
            lock (connections)
            {
                connections.Add(value);
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            lock (delays)
            {
                delays.Add((value, EventOf(tags)));
            }
        });
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            lock (sent)
            {
                sent.Add(instrument.Name + ":" + EventOf(tags));
            }
        });
        listener.Start();

        await TestSchedule.OpenEveryDayAsync(_fixture);
        var reception = await StaffAsync(StaffRole.Reception);
        listener.RecordObservableInstruments();
        Assert.True(connections.Count > 0 && connections[^1] >= 1, "Phải đếm được kết nối hub đang mở");

        var code = await ReservationApiClient.CodeOf(await new ReservationApiClient(_http).CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow)));
        await reception.NextAsync(RealtimeEventNames.ReservationCreated, Code(code));
        lock (delays)
        {
            Assert.Contains(delays, d => string.Equals(d.Event, RealtimeEventNames.ReservationCreated, StringComparison.Ordinal) && d.Ms >= 0 && d.Ms < 5000);
        }

        lock (sent)
        {
            Assert.Contains("obento.realtime.events.sent:" + RealtimeEventNames.ReservationCreated, sent, StringComparer.Ordinal);
        }
    }

    [Fact]
    public void Ve_song_30_giay_xem_khong_tieu_ve_doi_thi_tieu_ve()
    {
        var time = new ManualTime();
        var store = new HubTicketStore(time);
        var identity = new HubIdentity(HubIdentity.StaffKind, Guid.NewGuid(), nameof(StaffRole.Kitchen), "sv", DateTimeOffset.MaxValue);

        var first = store.Issue(identity)!.Value;
        Assert.Equal(identity, store.Peek(first.Ticket));
        Assert.Equal(identity, store.Redeem(first.Ticket));
        Assert.Null(store.Redeem(first.Ticket));

        var second = store.Issue(identity)!.Value;
        time.Advance(HubTicketStore.Lifetime);
        Assert.Null(store.Peek(second.Ticket));
        Assert.Null(store.Redeem(second.Ticket));
    }

    [Fact]
    public async Task Le_tan_huy_dat_ban_chi_phat_mot_su_kien_va_tra_cho()
    {
        var slotId = await TestSchedule.OpenEveryDayAsync(_fixture, maxReservations: 5);
        var token = await _admin.SignInAsync(StaffRole.Reception);
        var reception = await StaffAsync(token, track: true);
        var visitor = await VisitorAsync();
        Assert.True(await visitor.Connection.InvokeAsync<bool>("WatchAvailability", TestSchedule.Tomorrow));
        var api = new ReservationApiClient(_http);
        var code = await ReservationApiClient.CodeOf(await api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow)));
        await visitor.NextAsync(RealtimeEventNames.SlotAvailabilityChanged, p => p.GetProperty("available").GetInt32() == 4);
        await using var db = _fixture.CreateContext();
        var id = await db.Reservations.Where(r => r.ReservationCode == code).Select(r => r.Id).SingleAsync();

        var cancelled = await _admin.SendAsync(HttpMethod.Patch, $"/api/v1/admin/reservations/{id}/status", token, new { expectedStatus = "pending", status = "cancelled", reason = "Khách gọi hủy" });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        var changed = await reception.NextAsync(RealtimeEventNames.ReservationStatusChanged, Code(code));
        Assert.Equal("cancelled", changed.GetProperty("status").GetString());
        Assert.Equal(1, await reception.CountAsync(RealtimeEventNames.ReservationStatusChanged, Code(code)));
        await visitor.NextAsync(RealtimeEventNames.SlotAvailabilityChanged, p => p.GetProperty("slotId").GetInt32() == slotId && p.GetProperty("available").GetInt32() == 5);
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    [Fact]
    public async Task Don_moi_toi_bep_va_thu_ngan_khong_toi_le_tan_va_payload_khong_co_PII()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var kitchen = await StaffAsync(StaffRole.Kitchen);
        var cashier = await StaffAsync(StaffRole.Cashier);
        var reception = await StaffAsync(StaffRole.Reception);

        var (id, code, _) = await OrderAsync(menu);

        var payload = await kitchen.NextAsync(RealtimeEventNames.OrderCreated, Code(code));
        await cashier.NextAsync(RealtimeEventNames.OrderCreated, Code(code));
        Assert.False(await reception.ReceivedWithinAsync(RealtimeEventNames.OrderCreated, Code(code)));

        Assert.Equal(id, payload.GetProperty("id").GetGuid());
        Assert.Equal("pending", payload.GetProperty("status").GetString());
        var fields = payload.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["code", "id", "paymentStatus", "status"], fields);
    }

    [Fact]
    public async Task Khach_chi_nhan_su_kien_cua_don_minh_dang_xem()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var a = await OrderAsync(menu);
        var b = await OrderAsync(menu);
        var visitor = await VisitorAsync();

        Assert.False(await visitor.Connection.InvokeAsync<bool>("WatchOrder", b.Code, a.Token));
        Assert.True(await visitor.Connection.InvokeAsync<bool>("WatchOrder", a.Code, a.Token));

        Assert.Equal(HttpStatusCode.OK, (await CancelByStaffAsync(manager, b.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CancelByStaffAsync(manager, a.Id)).StatusCode);

        var changed = await visitor.NextAsync(RealtimeEventNames.OrderStatusChanged, Code(a.Code));
        Assert.Equal("cancelled", changed.GetProperty("status").GetString());
        Assert.False(await visitor.ReceivedWithinAsync(RealtimeEventNames.OrderStatusChanged, Code(b.Code)));
    }

    [Fact]
    public async Task Khach_dang_nhap_theo_doi_don_cua_minh_bang_ve_khong_xem_duoc_don_nguoi_khac()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var auth = new AuthApiClient(_http);
        var (owner, _) = await auth.RegisterAndLoginAsync(AuthApiClient.NewEmail());
        var mine = await OrderAsync(menu, owner);
        var other = await OrderAsync(menu);

        var anonymous = await VisitorAsync();
        Assert.False(await anonymous.Connection.InvokeAsync<bool>("WatchMyOrder", mine.Code));

        var member = await VisitorAsync(await CustomerTicketAsync(owner));
        Assert.False(await member.Connection.InvokeAsync<bool>("WatchMyOrder", other.Code));
        Assert.True(await member.Connection.InvokeAsync<bool>("WatchMyOrder", mine.Code));

        Assert.Equal(HttpStatusCode.OK, (await _orders.CancelAsync(mine.Code, mine.Token)).StatusCode);
        await member.NextAsync(RealtimeEventNames.OrderStatusChanged, Code(mine.Code));
    }

    [Fact]
    public async Task Dat_ban_moi_bao_le_tan_va_nguoi_dang_xem_ngay_do_nhan_so_cho_moi()
    {
        var slotId = await TestSchedule.OpenEveryDayAsync(_fixture, maxReservations: 5);
        var reception = await StaffAsync(StaffRole.Reception);
        var kitchen = await StaffAsync(StaffRole.Kitchen);
        var visitor = await VisitorAsync();
        var otherDay = await VisitorAsync();
        Assert.True(await visitor.Connection.InvokeAsync<bool>("WatchAvailability", TestSchedule.Tomorrow));
        Assert.True(await otherDay.Connection.InvokeAsync<bool>("WatchAvailability", TestSchedule.Tomorrow.AddDays(1)));
        Assert.False(await visitor.Connection.InvokeAsync<bool>("WatchAvailability", TestSchedule.TodayVn.AddDays(-1)));

        // Đổi sang ngày khác thì rời nhóm ngày cũ
        var switched = await VisitorAsync();
        Assert.True(await switched.Connection.InvokeAsync<bool>("WatchAvailability", TestSchedule.Tomorrow));
        Assert.True(await switched.Connection.InvokeAsync<bool>("WatchAvailability", TestSchedule.Tomorrow.AddDays(1)));

        var api = new ReservationApiClient(_http);
        var code = await ReservationApiClient.CodeOf(await api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow)));

        var created = await reception.NextAsync(RealtimeEventNames.ReservationCreated, Code(code));
        Assert.Equal(slotId, created.GetProperty("slotId").GetInt32());
        Assert.DoesNotContain("guest", created.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.False(await kitchen.ReceivedWithinAsync(RealtimeEventNames.ReservationCreated, Code(code)));

        // Người thứ hai đang xem cùng ngày thấy số chỗ mới, khớp với REST
        var rest = (await api.AvailabilityAsync(TestSchedule.Tomorrow)).GetProperty("slots").EnumerateArray()
            .Single(s => s.GetProperty("slotId").GetInt32() == slotId);
        var pushed = await visitor.NextAsync(RealtimeEventNames.SlotAvailabilityChanged, p => p.GetProperty("slotId").GetInt32() == slotId);
        Assert.Equal(rest.GetProperty("remainingReservations").GetInt32(), pushed.GetProperty("available").GetInt32());
        Assert.False(await otherDay.ReceivedWithinAsync(RealtimeEventNames.SlotAvailabilityChanged, _ => true));
        Assert.False(await switched.ReceivedWithinAsync(RealtimeEventNames.SlotAvailabilityChanged, _ => true, waitMs: 300));
    }

    [Fact]
    public async Task Lien_he_moi_bao_quan_ly_khong_bao_bep()
    {
        var manager = await StaffAsync(StaffRole.Manager);
        var kitchen = await StaffAsync(StaffRole.Kitchen);
        var email = AuthApiClient.NewEmail();

        var body = new { fullName = "Trần Bình", email, phoneNumber = "0901234567", subject = "Đặt tiệc", message = $"tiec-{Guid.NewGuid():N}", lang = "vi", website = (string?)null };
        Assert.Equal(HttpStatusCode.Accepted, (await _http.PostAsJsonAsync("/api/v1/contact", body)).StatusCode);
        await using var db = _fixture.CreateContext();
        var id = await db.ContactMessages.Where(m => m.Email == email).Select(m => m.Id).SingleAsync();

        await manager.NextAsync(RealtimeEventNames.ContactCreated, p => p.GetProperty("id").GetInt32() == id);
        Assert.False(await kitchen.ReceivedWithinAsync(RealtimeEventNames.ContactCreated, p => p.GetProperty("id").GetInt32() == id));
    }

    [Fact]
    public async Task Su_kien_chi_phat_sau_commit_hoan_tac_hoac_loi_thi_bo()
    {
        var manager = await StaffAsync(StaffRole.Manager);
        static RealtimeMessage Contact(int id) => new(RealtimeEventNames.ContactCreated, [RealtimeGroups.Staff(StaffRole.Manager)], new ContactEventDto(id));
        static Func<JsonElement, bool> Id(int id) => p => p.GetProperty("id").GetInt32() == id;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var realtime = scope.ServiceProvider.GetRequiredService<IRealtimeEvents>();

            await unitOfWork.TryInTransactionAsync(_ =>
            {
                realtime.Stage(Contact(-1));
                return Task.FromResult(Result.Failure<bool>(ErrorCodes.ValidationFailed));
            }, CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.InTransactionAsync<bool>(_ =>
            {
                realtime.Stage(Contact(-2));
                throw new InvalidOperationException("lỗi giữa chừng");
            }, CancellationToken.None));

            // Lưu ngoài transaction bị lỗi (trùng khóa) cũng bỏ sự kiện đã gom
            realtime.Stage(Contact(-4));
            var db = scope.ServiceProvider.GetRequiredService<ObentoTensho.Infrastructure.Persistence.AppDbContext>();
            db.SystemSettings.Add(new ObentoTensho.Core.Settings.Domain.SystemSetting { Key = ObentoTensho.Core.Settings.Domain.SettingKeys.EmailDailyQuota, Value = "1", ValueType = SettingType.Number, Description = "trùng" });
            await Assert.ThrowsAsync<DuplicateKeyException>(() => unitOfWork.SaveChangesAsync(CancellationToken.None));

            // Lưu ngoài transaction sau đó không được kéo theo sự kiện của các lần hỏng
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            await unitOfWork.InTransactionAsync(_ =>
            {
                realtime.Stage(Contact(-3));
                return Task.FromResult(true);
            }, CancellationToken.None);
        }

        await manager.NextAsync(RealtimeEventNames.ContactCreated, Id(-3));
        Assert.False(await manager.ReceivedWithinAsync(RealtimeEventNames.ContactCreated, p => Id(-1)(p) || Id(-2)(p) || Id(-4)(p)));
    }

    [Fact]
    public async Task Moi_nhan_vien_toi_da_5_ket_noi()
    {
        var token = await _admin.SignInAsync(StaffRole.Kitchen);
        for (var i = 0; i < HubConnectionTracker.MaxPerUser; i++)
        {
            Assert.Equal(HubConnectionState.Connected, (await StaffAsync(token, track: true)).Connection.State);
        }

        await using var sixth = HubTestClient.Create(_factory, HubRoutes.Staff, ApiFactory.AdminOrigin, await TicketAsync(token));
        Assert.True(await RejectedAsync(sixth));

        // Đóng một tab thì mở lại được
        await _clients[^1].Connection.StopAsync();
        var reopened = await StaffAsync(token, track: true);
        Assert.False(await reopened.ClosedWithinAsync(1500));
    }

    [Fact]
    public async Task Doi_mat_khau_thi_ket_noi_dang_mo_bi_ngat()
    {
        var token = await _admin.SignInAsync(StaffRole.Cashier);
        var other = await StaffAsync(StaffRole.Cashier);
        var victim = await StaffAsync(token, track: true);
        var sweeper = _factory.Services.GetRequiredService<HubSessionSweeper>();

        Assert.Equal(0, await sweeper.SweepAsync(CancellationToken.None));

        var changed = await _admin.SendAsync(HttpMethod.Post, "/api/v1/admin/auth/me/change-password", token,
            new { currentPassword = AdminAuthApiClient.Password, newPassword = "Doi-mat-khau-moi-2026" });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        // Trạng thái phiên được nhớ tối đa 5 giây (SEC-50); bộ quét thật chạy 30 giây một lần
        var closed = 0;
        for (var i = 0; i < 20 && closed == 0; i++)
        {
            await Task.Delay(500);
            closed = await sweeper.SweepAsync(CancellationToken.None);
        }

        Assert.Equal(1, closed);
        Assert.True(await victim.ClosedWithinAsync());
        Assert.Equal(HubConnectionState.Connected, other.Connection.State);
    }

    [Fact]
    public async Task Moi_ket_noi_bi_gioi_han_so_lenh_gui_len()
    {
        var visitor = await VisitorAsync();
        for (var i = 0; i < HubInvocationLimitFilter.MaxPerWindow; i++)
        {
            Assert.True(await visitor.Connection.InvokeAsync<bool>("WatchAvailability", TestSchedule.Tomorrow));
        }

        var error = await Assert.ThrowsAsync<HubException>(() => visitor.Connection.InvokeAsync<bool>("WatchAvailability", TestSchedule.Tomorrow));
        Assert.Contains(ErrorCodes.RateLimited, error.Message, StringComparison.Ordinal);
    }
}
