using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Reservations;

// FR-RSV-02/05/06, BR-RSV-12/23: lễ tân gọi xác minh, đổi trạng thái, gán bàn, tạo hộ khách
[Collection(SqlServerFixture.CollectionName)]
public sealed class ReservationDeskTests : IDisposable
{
    private const string Admin = "/api/v1/admin/reservations";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly ReservationApiClient _public;
    private readonly AdminAuthApiClient _admin;

    public ReservationDeskTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _public = new ReservationApiClient(_http);
        _admin = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Le_tan_phai_goi_xac_minh_truoc_khi_xac_nhan_roi_check_in_hoan_thanh()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var reception = await _admin.SignInAsync(StaffRole.Reception);
        var (id, version) = await CreatePublicAsync();

        var early = await ChangeStatus(reception, id, ReservationStatus.Pending, ReservationStatus.Confirmed);
        Assert.Equal(ErrorCodes.ContactNotConfirmed, await AuthApiClient.ReadErrorCodeAsync(early));

        var contact = await AdminData(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/{id}/reveal-contact", reception));
        Assert.StartsWith("+84", contact.GetProperty("phone").GetString(), StringComparison.Ordinal);
        (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/{id}/contact-confirmation", reception, new { note = "Khách đến muộn 10 phút" })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await ChangeStatus(reception, id, ReservationStatus.Pending, ReservationStatus.Confirmed)).StatusCode);
        Assert.Equal(ErrorCodes.InvalidTransition, await AuthApiClient.ReadErrorCodeAsync(await ChangeStatus(reception, id, ReservationStatus.Pending, ReservationStatus.Confirmed)));
        Assert.Equal(HttpStatusCode.OK, (await ChangeStatus(reception, id, ReservationStatus.Confirmed, ReservationStatus.CheckedIn)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ChangeStatus(reception, id, ReservationStatus.CheckedIn, ReservationStatus.Completed)).StatusCode);
        Assert.Equal(ErrorCodes.InvalidTransition, await AuthApiClient.ReadErrorCodeAsync(await ChangeStatus(reception, id, ReservationStatus.Completed, ReservationStatus.Confirmed)));

        var detail = await AdminData(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/{id}", reception));
        Assert.Equal(4, detail.GetProperty("history").GetArrayLength());
        Assert.Equal("Khách đến muộn 10 phút", detail.GetProperty("reservation").GetProperty("confirmNote").GetString());
        Assert.NotNull(version);

        await using var db = _fixture.CreateContext();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "reservations.reservation.contact_revealed" && a.EntityId == id.ToString()));
    }

    [Fact]
    public async Task Tu_choi_bat_buoc_ly_do_tra_cho_va_gui_thu()
    {
        var slotId = await TestSchedule.OpenEveryDayAsync(_fixture);
        var reception = await _admin.SignInAsync(StaffRole.Reception);
        var (id, _) = await CreatePublicAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeStatus(reception, id, ReservationStatus.Pending, ReservationStatus.Cancelled)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ChangeStatus(reception, id, ReservationStatus.Pending, ReservationStatus.Cancelled, "Ca đã kín chỗ")).StatusCode);

        await using var db = _fixture.CreateContext();
        Assert.Equal(0, await db.SlotDayCapacity.Where(c => c.SlotId == slotId).Select(c => c.BookedCount).SingleAsync());
        Assert.True(await db.OutboxMessages.AnyAsync(m => m.PayloadJson.Contains("reservation_rejected") && m.PayloadJson.Contains(id.ToString())));
    }

    [Fact]
    public async Task Le_tan_tao_ho_qua_dien_thoai_va_chi_quan_ly_duoc_vuot_suc_chua()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture, maxReservations: 1);
        var reception = await _admin.SignInAsync(StaffRole.Reception);
        var manager = await _admin.SignInAsync(StaffRole.Manager);

        var phone = await _admin.SendAsync(HttpMethod.Post, Admin, reception, StaffBooking(overrideReason: null));
        Assert.Equal(HttpStatusCode.Created, phone.StatusCode);
        var data = await AdminData(phone);
        Assert.Equal("confirmed", data.GetProperty("status").GetString());
        Assert.Equal("phone", data.GetProperty("source").GetString());
        Assert.True(data.GetProperty("contactConfirmed").GetBoolean());

        Assert.Equal(ErrorCodes.SlotFull, await AuthApiClient.ReadErrorCodeAsync(await _admin.SendAsync(HttpMethod.Post, Admin, reception, StaffBooking(null))));
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Post, Admin, reception, StaffBooking("Khách quen"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _admin.SendAsync(HttpMethod.Post, Admin, manager, StaffBooking("Khách quen"))).StatusCode);
    }

    [Fact]
    public async Task Gan_ban_kiem_suc_chua_va_trung_ca_khong_xoa_ban_dang_co_dat_ban()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var area = await AdminData(await _admin.SendAsync(HttpMethod.Post, "/api/v1/admin/table-areas", manager, new { name = $"Sân {Guid.NewGuid():N}", isActive = true }));
        var table = await AdminData(await _admin.SendAsync(HttpMethod.Post, "/api/v1/admin/tables", manager, new
        {
            areaId = area.GetProperty("id").GetInt32(), name = "S1", minCapacity = 2, maxCapacity = 4,
            layout = new { x = 0, y = 0, width = 60, height = 60, rotation = 0 }, isActive = true,
        }));
        var tableId = table.GetProperty("id").GetInt32();
        var (first, _) = await CreatePublicAsync(adults: 3);
        var (second, _) = await CreatePublicAsync(adults: 2);
        var (big, _) = await CreatePublicAsync(adults: 6);

        Assert.Equal(HttpStatusCode.OK, (await Assign(manager, first, tableId)).StatusCode);
        Assert.Equal(ErrorCodes.TableConflict, await AuthApiClient.ReadErrorCodeAsync(await Assign(manager, second, tableId)));
        Assert.Equal(ErrorCodes.TableUnsuitable, await AuthApiClient.ReadErrorCodeAsync(await Assign(manager, big, tableId)));
        Assert.Equal(ErrorCodes.TableHasReservations, await AuthApiClient.ReadErrorCodeAsync(
            await _admin.SendAsync(HttpMethod.Delete, $"/api/v1/admin/tables/{tableId}", manager, ifMatch: $"\"{table.GetProperty("version").GetString()}\"")));
    }

    [Fact]
    public async Task Danh_sach_che_lien_he_va_bep_khong_vao_duoc()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var reception = await _admin.SignInAsync(StaffRole.Reception);
        var kitchen = await _admin.SignInAsync(StaffRole.Kitchen);
        await CreatePublicAsync();

        var list = await _admin.SendAsync(HttpMethod.Get, $"{Admin}?from={ReservationApiClient.Date(TestSchedule.Tomorrow)}&pageSize=500", reception);
        var body = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(100, body.GetProperty("meta").GetProperty("pageSize").GetInt32());
        Assert.Contains("****", body.GetProperty("data")[0].GetProperty("maskedPhone").GetString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, Admin, kitchen)).StatusCode);
    }

    private async Task<(Guid Id, string? Version)> CreatePublicAsync(int adults = 2)
    {
        var created = await _public.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow, adults: adults));
        created.EnsureSuccessStatusCode();
        var code = await ReservationApiClient.CodeOf(created);
        await using var db = _fixture.CreateContext();
        var reservation = await db.Reservations.AsNoTracking().SingleAsync(r => r.ReservationCode == code);
        return (reservation.Id, Convert.ToBase64String(reservation.RowVersion));
    }

    private Task<HttpResponseMessage> ChangeStatus(string token, Guid id, ReservationStatus expected, ReservationStatus status, string? reason = null) =>
        _admin.SendAsync(HttpMethod.Patch, $"{Admin}/{id}/status", token, new { expectedStatus = Snake(expected), status = Snake(status), reason });

    private Task<HttpResponseMessage> Assign(string token, Guid id, int tableId) =>
        _admin.SendAsync(HttpMethod.Patch, $"{Admin}/{id}/assign-table", token, new { tableId });

    private static object StaffBooking(string? overrideReason) => new
    {
        date = ReservationApiClient.Date(TestSchedule.Tomorrow),
        time = "11:30",
        adultCount = 4,
        childCount = 1,
        guestName = "Lê Văn Cường",
        guestPhone = ReservationApiClient.NewPhone(),
        guestEmail = (string?)null,
        specialRequest = (string?)null,
        internalNote = "Khách gọi điện đặt",
        source = "phone",
        checkInNow = false,
        overrideCapacityReason = overrideReason,
    };

    private static string Snake(ReservationStatus status) => JsonNamingPolicy.SnakeCaseLower.ConvertName(status.ToString());

    private static async Task<JsonElement> AdminData(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await AdminAuthApiClient.ReadDataAsync(response);
    }
}
