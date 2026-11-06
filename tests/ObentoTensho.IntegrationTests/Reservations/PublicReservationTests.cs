using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Tables.Domain;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Reservations;

// docs/srs/11 §11: khách đặt bàn, tra cứu, hủy; giữ chỗ nguyên tử; các mã lỗi lịch
[Collection(SqlServerFixture.CollectionName)]
public sealed class PublicReservationTests : IDisposable
{
    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly ReservationApiClient _api;

    public PublicReservationTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _api = new ReservationApiClient(_http);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Dat_ban_tra_cuu_va_huy_bang_lien_ket_trong_thu()
    {
        var slotId = await TestSchedule.OpenEveryDayAsync(_fixture);
        var email = AuthApiClient.NewEmail();

        var availability = await _api.AvailabilityAsync(TestSchedule.Tomorrow);
        Assert.True(availability.GetProperty("open").GetBoolean());
        Assert.Equal(16, availability.GetProperty("slots")[0].GetProperty("times").GetArrayLength());

        var created = await _api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow, phone: "0912345678", email: email));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var data = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("091****678", data.GetProperty("maskedPhone").GetString());
        Assert.Equal("pending", data.GetProperty("status").GetString());
        var code = data.GetProperty("code").GetString() ?? string.Empty;
        Assert.Equal(1, await BookedCountAsync(slotId));

        Assert.Equal(HttpStatusCode.OK, (await _api.LookupAsync(code.ToLowerInvariant(), "5678")).StatusCode);
        Assert.Equal(ErrorCodes.NotFound, await AuthApiClient.ReadErrorCodeAsync(await _api.LookupAsync(code, "0000")));

        await _factory.ProcessOutboxAsync();
        var token = ReservationApiClient.TokenIn(_factory.Emails, email);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.CancelAsync(code, "token-sai")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.CancelAsync(code, token)).StatusCode);
        Assert.Equal(ErrorCodes.InvalidTransition, await AuthApiClient.ReadErrorCodeAsync(await _api.CancelAsync(code, token)));
        Assert.Equal(0, await BookedCountAsync(slotId));
    }

    [Fact]
    public async Task Tra_cuu_sai_5_lan_thi_khoa_ma()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var code = await ReservationApiClient.CodeOf(await _api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow, phone: "0912000111")));

        for (var i = 0; i < 5; i++)
        {
            await _api.LookupAsync(code, "9999");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await _api.LookupAsync(code, "0111")).StatusCode);
    }

    [Fact]
    public async Task Hai_muoi_yeu_cau_dong_thoi_vao_ca_con_1_cho_chi_1_thanh_cong()
    {
        var slotId = await TestSchedule.OpenEveryDayAsync(_fixture, maxReservations: 1);

        var responses = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => _api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(19, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await BookedCountAsync(slotId));
        await using var db = _fixture.CreateContext();
        Assert.Equal(1, await db.Reservations.CountAsync());
    }

    [Fact]
    public async Task Moi_SDT_toi_da_2_dat_ban_dang_hoat_dong_ke_ca_viet_khac_dang()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);

        Assert.Equal(HttpStatusCode.Created, (await _api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow, phone: "0912345111"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow.AddDays(1), phone: "+84 912 345 111"))).StatusCode);
        Assert.Equal(ErrorCodes.ReservationLimit, await AuthApiClient.ReadErrorCodeAsync(
            await _api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow.AddDays(2), phone: "84912345111"))));
    }

    [Fact]
    public async Task Ngay_qua_khu_ngoai_ca_bi_khoa_ngay_le_tra_dung_ma_loi()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var holiday = TestSchedule.Tomorrow.AddDays(2);
        await using (var db = _fixture.CreateContext())
        {
            var staffId = await db.StaffMembers.Select(s => s.Id).FirstOrDefaultAsync();
            if (staffId == Guid.Empty)
            {
                staffId = (await new AdminAuthApiClient(_factory, _fixture).CreateStaffAsync(StaffRole.Manager)).Id;
            }

            db.BlockedDates.Add(new BlockedDate
            {
                Date = TestSchedule.Tomorrow, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(13, 0),
                AppliesTo = BlockScope.Reservation, CreatedByStaffId = staffId, CreatedAtUtc = DateTime.UtcNow,
            });
            db.SpecialHours.Add(new SpecialHour { Date = holiday, IsClosed = true, CreatedByStaffId = staffId });
            await db.SaveChangesAsync();
        }

        Assert.Equal(ErrorCodes.DateOutOfRange, await ErrorOf(TestSchedule.TodayVn.AddDays(-1), "12:00"));
        Assert.Equal(ErrorCodes.OutsideServiceSlot, await ErrorOf(TestSchedule.Tomorrow, "18:00"));
        Assert.Equal(ErrorCodes.DateBlocked, await ErrorOf(TestSchedule.Tomorrow, "12:30"));
        Assert.Equal(HttpStatusCode.Created, (await _api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow, "13:00"))).StatusCode);
        Assert.Equal(ErrorCodes.OutsideOpeningHours, await ErrorOf(holiday, "12:00"));
        Assert.False((await _api.AvailabilityAsync(holiday)).GetProperty("open").GetBoolean());
    }

    [Fact]
    public async Task SDT_co_2_lan_khong_den_trong_90_ngay_phai_xac_minh_OTP()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        const string phone = "0912999888";
        for (var i = 0; i < 2; i++)
        {
            (await _api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow.AddDays(i), phone: phone))).EnsureSuccessStatusCode();
        }

        await using (var db = _fixture.CreateContext())
        {
            await db.Reservations.Where(r => r.GuestPhone == "+84912999888")
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReservationStatus.NoShow));
        }

        Assert.Equal(ErrorCodes.OtpRequired, await AuthApiClient.ReadErrorCodeAsync(
            await _api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow.AddDays(3), phone: phone))));
    }

    private async Task<string?> ErrorOf(DateOnly date, string time) =>
        await AuthApiClient.ReadErrorCodeAsync(await _api.CreateAsync(ReservationApiClient.Booking(date, time)));

    private async Task<int> BookedCountAsync(int slotId)
    {
        await using var db = _fixture.CreateContext();
        return await db.SlotDayCapacity.Where(c => c.SlotId == slotId && c.Date == TestSchedule.Tomorrow).Select(c => c.BookedCount).SingleAsync();
    }
}
