using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Loyalty.Services;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;

namespace ObentoTensho.IntegrationTests.Loyalty;

// docs/srs/13 §9: tích điểm một lần, nâng hạng và giảm giá theo hạng, thu hồi có nợ điểm, hết hạn FIFO, điều chỉnh bốn mắt
[Collection(SqlServerFixture.CollectionName)]
public sealed class LoyaltyTests : IDisposable
{
    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public LoyaltyTests(SqlServerFixture fixture)
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

    [Fact]
    public async Task Don_hoan_thanh_cong_diem_mot_lan_nang_hang_va_don_sau_duoc_giam()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var (userId, token, email) = await MemberAsync();
        await SetPointsAsync(userId, accumulated: 290);
        var cashier = await _admin.SignInAsync(StaffRole.Manager);
        var orders = new OrderApiClient(_http);

        // (65.000 + 15.000) × 2 = 160.000 ₫ ⇒ 16 điểm; 290 + 16 = 306 ⇒ lên Bạc
        var created = await OrderApiClient.DataOf(await orders.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Rice, 2, menu.Large)], email: email), token));
        Assert.Equal(0m, created.GetProperty("order").GetProperty("memberDiscount").GetDecimal());
        var code = created.GetProperty("order").GetProperty("code").GetString()!;
        var id = await OrderIdAsync(code);
        await CompleteAsync(cashier, id);

        var summary = await AccountAsync(token, "/api/v1/account/loyalty-points");
        Assert.Equal((16, 306, "silver", 3), (summary.GetProperty("currentPoints").GetInt32(), summary.GetProperty("accumulatedPoints").GetInt32(), summary.GetProperty("tier").GetString(), summary.GetProperty("discountPercent").GetInt32()));
        Assert.Equal(694, summary.GetProperty("pointsToNextTier").GetInt32());

        // Cộng lại cho cùng đơn (vd lỗi gọi lặp) bị bỏ qua (BR-LOY-02)
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var ledger = scope.ServiceProvider.GetRequiredService<LoyaltyLedger>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            Assert.Equal(0, await unitOfWork.InTransactionAsync(ct => ledger.EarnAsync(userId, LoyaltyTransactionType.OrderEarn, code, 16, null, ct), CancellationToken.None));
        }

        // Thành viên Bạc: đơn sau giảm 3% (BR-ORD-03)
        var quote = await OrderApiClient.DataOf(await PostAsync("/api/v1/orders/quote", token, new { items = new[] { OrderApiClient.Line(menu.Rice, 1, menu.Small) }, lang = "vi" }));
        Assert.Equal((1_950m, 63_050m), (quote.GetProperty("memberDiscount").GetDecimal(), quote.GetProperty("totalAmount").GetDecimal()));

        await _factory.ProcessOutboxAsync();
        var mails = _factory.Emails.To(email);
        Assert.Contains(mails, m => m.Subject.Contains("16 điểm", StringComparison.Ordinal));
        Assert.Contains(mails, m => m.Subject.Contains("Bạc", StringComparison.Ordinal));

        await using var db = _fixture.CreateContext();
        Assert.Equal(16, (await db.Orders.SingleAsync(o => o.Id == id)).PointsEarned);
    }

    [Fact]
    public async Task Cong_diem_dong_thoi_cho_cung_su_kien_chi_ghi_mot_lan()
    {
        var (userId, _, _) = await MemberAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => TryEarnAsync(userId, "OBT-TK-SONG-SONG", 25)));

        Assert.Equal(25, results.Sum());
        await using var db = _fixture.CreateContext();
        Assert.Equal(1, await db.LoyaltyPoints.CountAsync(e => e.UserId == userId));
        Assert.Equal(25, (await db.Users.SingleAsync(u => u.Id == userId)).CurrentPoints);
    }

    [Fact]
    public async Task Thu_hoi_khi_diem_da_tieu_thi_ghi_no_va_lan_tich_sau_tra_no_truoc()
    {
        var (userId, _, _) = await MemberAsync();
        await RunAsync((ledger, ct) => ledger.EarnAsync(userId, LoyaltyTransactionType.OrderEarn, "OBT-TK-THU-HOI", 100, null, ct));
        await RunAsync(async (ledger, ct) => (await ledger.AdjustAsync(Adjustment(userId, -70), ct)).IsSuccess ? 1 : 0);

        // Còn 30 mà thu hồi 100 ⇒ về 0, nợ 70 (BR-LOY-08)
        Assert.Equal(100, await RunAsync((ledger, ct) => ledger.RevokeAsync(userId, LoyaltyTransactionType.OrderEarn, "OBT-TK-THU-HOI", ct)));
        Assert.Equal(0, await RunAsync((ledger, ct) => ledger.RevokeAsync(userId, LoyaltyTransactionType.OrderEarn, "OBT-TK-THU-HOI", ct)));
        Assert.Equal((0, 70), await PointsAsync(userId));

        await RunAsync((ledger, ct) => ledger.EarnAsync(userId, LoyaltyTransactionType.OrderEarn, "OBT-TK-SAU-NO", 50, null, ct));
        Assert.Equal((0, 20), await PointsAsync(userId));

        await using var db = _fixture.CreateContext();
        var types = await db.LoyaltyPoints.Where(e => e.UserId == userId).OrderBy(e => e.Id).Select(e => e.Type).ToListAsync();
        Assert.Equal(
            [LoyaltyTransactionType.OrderEarn, LoyaltyTransactionType.AdminAdjust, LoyaltyTransactionType.Revoke, LoyaltyTransactionType.OrderEarn, LoyaltyTransactionType.DebtRepaid],
            types);
        Assert.Equal(0, await db.LoyaltyLots.Where(l => l.UserId == userId).SumAsync(l => l.RemainingPoints));
    }

    [Fact]
    public async Task Het_han_theo_FIFO_chi_phan_chua_tieu()
    {
        // +100 (hạn đã qua), +50 (còn hạn), đã dùng 100 ⇒ dùng từ lô cũ trước nên không mất điểm nào
        var (spent, _, _) = await MemberAsync();
        await EarnAgedAsync(spent);
        await RunAsync(async (ledger, ct) => (await ledger.AdjustAsync(Adjustment(spent, -100), ct)).IsSuccess ? 1 : 0);

        // Cùng tình huống nhưng chưa dùng ⇒ mất 100 điểm của lô cũ
        var (kept, _, _) = await MemberAsync();
        await EarnAgedAsync(kept);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<LoyaltyJobs>().ExpirePointsAsync(CancellationToken.None);
        }

        Assert.Equal((50, 0), await PointsAsync(spent));
        Assert.Equal((50, 0), await PointsAsync(kept));
        await using var db = _fixture.CreateContext();
        Assert.False(await db.LoyaltyPoints.AnyAsync(e => e.UserId == spent && e.Type == LoyaltyTransactionType.Expired));
        Assert.Equal(-100, await db.LoyaltyPoints.Where(e => e.UserId == kept && e.Type == LoyaltyTransactionType.Expired).SumAsync(e => e.Points));
        Assert.Equal(150, (await db.Users.SingleAsync(u => u.Id == kept)).AccumulatedPoints);
    }

    [Fact]
    public async Task Dieu_chinh_tay_can_ly_do_khong_tru_qua_so_du_va_vuot_nguong_can_nguoi_khac_duyet()
    {
        var (userId, _, _) = await MemberAsync();
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var other = await _admin.SignInAsync(StaffRole.Manager);
        var path = $"/api/v1/admin/customers/{userId}/loyalty-points";

        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Patch, path, manager, new { points = 300, reason = " " })).StatusCode);
        Assert.Equal("approved", (await Data(_admin.SendAsync(HttpMethod.Patch, path, manager, new { points = 300, reason = "Bù điểm đơn lỗi" }))).GetProperty("status").GetString());
        Assert.Equal(ErrorCodes.PointsInsufficient, await AuthApiClient.ReadErrorCodeAsync(await _admin.SendAsync(HttpMethod.Patch, path, manager, new { points = -500, reason = "Trừ nhầm" })));

        var pending = await Data(_admin.SendAsync(HttpMethod.Patch, path, manager, new { points = 1500, reason = "Khách tiệc công ty" }));
        Assert.Equal("pending", pending.GetProperty("status").GetString());
        Assert.Equal((300, 0), await PointsAsync(userId));

        var approve = $"/api/v1/admin/loyalty/adjustments/{pending.GetProperty("id").GetGuid()}/approve";
        // SEC-61: duyệt điều chỉnh điểm lớn phải nhập lại mật khẩu/mã 2FA
        Assert.Equal(ErrorCodes.StepUpRequired, await AuthApiClient.ReadErrorCodeAsync(await _admin.SendAsync(HttpMethod.Post, approve, other, new { note = (string?)null })));
        Assert.Equal((300, 0), await PointsAsync(userId));
        Assert.Equal(ErrorCodes.FourEyesRequired, await AuthApiClient.ReadErrorCodeAsync(await _admin.SendAsync(HttpMethod.Post, approve, manager, new { note = (string?)null }, stepUp: await _admin.StepUpAsync(manager))));
        var stepUp = await _admin.StepUpAsync(other);
        Assert.Equal("approved", (await Data(_admin.SendAsync(HttpMethod.Post, approve, other, new { note = "Đã xem hợp đồng" }, stepUp: stepUp))).GetProperty("status").GetString());
        Assert.Equal(ErrorCodes.InvalidTransition, await AuthApiClient.ReadErrorCodeAsync(await _admin.SendAsync(HttpMethod.Post, approve, other, new { note = (string?)null }, stepUp: stepUp)));
        Assert.Equal((1800, 0), await PointsAsync(userId));

        // Lễ tân không có quyền khách hàng
        var reception = await _admin.SignInAsync(StaffRole.Reception);
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, path, reception)).StatusCode);

        await using var db = _fixture.CreateContext();
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "loyalty.adjustment.applied" && a.NewDataJson!.Contains(userId.ToString())));
    }

    [Fact]
    public async Task Check_in_dat_ban_cong_diem_toi_da_30_moi_thang()
    {
        var (userId, _, _) = await MemberAsync();
        var points = new List<int>();
        for (var i = 0; i < 4; i++)
        {
            points.Add(await RunAsync((ledger, ct) => ledger.EarnAsync(userId, LoyaltyTransactionType.ReservationEarn, $"OBT-CHECKIN-{i}", 10, null, ct)));
        }

        Assert.Equal([10, 10, 10, 10], points);

        // Qua ReservationTransitions: đã có 40 điểm check-in tháng này ⇒ lần sau không cộng (BR-LOY-22)
        var slotId = await TestSchedule.OpenEveryDayAsync(_fixture);
        var reservationId = await ConfirmedReservationAsync(userId, slotId);
        var staffId = (await _admin.CreateStaffAsync(StaffRole.Reception)).Id;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var transitions = scope.ServiceProvider.GetRequiredService<Core.Reservations.Services.ReservationTransitions>();
            var reservations = scope.ServiceProvider.GetRequiredService<Core.Reservations.Abstractions.IReservationRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.InTransactionAsync(async ct =>
            {
                var reservation = await reservations.FindForUpdateAsync(reservationId, ct);
                Assert.True((await transitions.ApplyAsync(reservation!, ReservationStatus.CheckedIn, ReservationActor.Staff, staffId, null, ct)).IsSuccess);
                await unitOfWork.SaveChangesAsync(ct);
                return true;
            }, CancellationToken.None);
        }

        await using var db = _fixture.CreateContext();
        Assert.Equal(0, (await db.Reservations.SingleAsync(r => r.Id == reservationId)).PointsEarned);
        Assert.Equal(40, (await db.Users.SingleAsync(u => u.Id == userId)).CurrentPoints);
    }

    private static Core.Loyalty.Domain.LoyaltyAdjustment Adjustment(Guid userId, int points) => new()
    {
        UserId = userId,
        Points = points,
        Reason = "Kiểm thử",
        RequestedByStaffId = Guid.Empty,
        RequestedAtUtc = DateTime.UtcNow,
    };

    private async Task<(Guid Id, string Token, string Email)> MemberAsync()
    {
        var email = AuthApiClient.NewEmail();
        var (token, _) = await new AuthApiClient(_http).RegisterAndLoginAsync(email);
        await using var db = _fixture.CreateContext();
        return (await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync(), token, email);
    }

    private async Task SetPointsAsync(Guid userId, int accumulated)
    {
        await using var db = _fixture.CreateContext();
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccumulatedPoints, accumulated));
    }

    private async Task EarnAgedAsync(Guid userId)
    {
        await RunAsync((ledger, ct) => ledger.EarnAsync(userId, LoyaltyTransactionType.OrderEarn, $"CU-{userId:N}", 100, null, ct));
        await using (var db = _fixture.CreateContext())
        {
            await db.LoyaltyLots.Where(l => l.UserId == userId).ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAtUtc, DateTime.UtcNow.AddMonths(-1)));
        }

        await RunAsync((ledger, ct) => ledger.EarnAsync(userId, LoyaltyTransactionType.OrderEarn, $"MOI-{userId:N}", 50, null, ct));
    }

    private async Task<int> RunAsync(Func<LoyaltyLedger, CancellationToken, Task<int>> action)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<LoyaltyLedger>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await unitOfWork.InTransactionAsync(async ct =>
        {
            var result = await action(ledger, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return result;
        }, CancellationToken.None);
    }

    private async Task<int> TryEarnAsync(Guid userId, string reference, int points)
    {
        try
        {
            return await RunAsync((ledger, ct) => ledger.EarnAsync(userId, LoyaltyTransactionType.OrderEarn, reference, points, null, ct));
        }
        catch (DuplicateKeyException)
        {
            return 0;
        }
    }

    private async Task<(int Current, int Debt)> PointsAsync(Guid userId)
    {
        await using var db = _fixture.CreateContext();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        return (user.CurrentPoints, user.PointsDebt);
    }

    private async Task<Guid> OrderIdAsync(string code)
    {
        await using var db = _fixture.CreateContext();
        return await db.Orders.Where(o => o.OrderCode == code).Select(o => o.Id).SingleAsync();
    }

    private async Task CompleteAsync(string token, Guid id)
    {
        const string admin = "/api/v1/admin/orders";
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Patch, $"{admin}/{id}/contact-confirmed", token, new { note = (string?)null })).StatusCode);
        foreach (var (from, to) in new[] { ("pending", "preparing"), ("preparing", "ready") })
        {
            Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Patch, $"{admin}/{id}/status", token, new { expectedStatus = from, status = to })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Patch, $"{admin}/{id}/payment", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Patch, $"{admin}/{id}/status", token, new { expectedStatus = "ready", status = "completed" })).StatusCode);
    }

    private async Task<Guid> ConfirmedReservationAsync(Guid userId, int slotId)
    {
        await using var db = _fixture.CreateContext();
        var tomorrow = TestSchedule.Tomorrow;
        var reservation = new Core.Reservations.Domain.Reservation
        {
            ReservationCode = Core.Reservations.Domain.ReservationCodes.New(),
            DailySeq = 0,
            UserId = userId,
            GuestName = "Thành viên",
            GuestPhone = "+84" + OrderApiClient.NewPhone()[1..],
            Lang = "vi",
            ReservedDate = tomorrow,
            ReservedTime = new TimeOnly(12, 0),
            ReservedAtUtc = tomorrow.ToDateTime(new TimeOnly(5, 0), DateTimeKind.Utc),
            SlotId = slotId,
            AdultCount = 2,
            Status = ReservationStatus.Confirmed,
            ContactConfirmedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();
        return reservation.Id;
    }

    private async Task<JsonElement> AccountAsync(string token, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return (await (await _http.SendAsync(request)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private Task<HttpResponseMessage> PostAsync(string path, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return _http.SendAsync(request);
    }

    private static async Task<JsonElement> Data(Task<HttpResponseMessage> request) =>
        await AdminAuthApiClient.ReadDataAsync(await request);
}
