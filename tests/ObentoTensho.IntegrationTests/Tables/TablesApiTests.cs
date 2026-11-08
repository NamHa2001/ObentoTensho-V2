using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Tables;

// docs/srs/16 §8 và BR-TBL-14/15/16: bàn, khu vực, ca, khóa ngày, giờ mở cửa qua API thật
[Collection(SqlServerFixture.CollectionName)]
public sealed class TablesApiTests : IDisposable
{
    private const string Admin = "/api/v1/admin/";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly AdminAuthApiClient _api;

    public TablesApiTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _api = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Khu_vuc_va_ban_CRUD_co_ETag_quyen_va_audit()
    {
        var manager = await _api.SignInAsync(StaffRole.Manager);
        var reception = await _api.SignInAsync(StaffRole.Reception);

        var area = await _api.SendAsync(HttpMethod.Post, Admin + "table-areas", manager, new { name = $"Tầng {Guid.NewGuid():N}", isActive = true });
        Assert.Equal(HttpStatusCode.Created, area.StatusCode);
        var areaId = (await AdminAuthApiClient.ReadDataAsync(area)).GetProperty("id").GetInt32();
        var areaETag = area.Headers.ETag?.Tag;

        // Lễ tân tạo bàn được, sửa bàn thì không
        var table = await _api.SendAsync(HttpMethod.Post, Admin + "tables", reception, TableBody(areaId, "B1"));
        Assert.Equal(HttpStatusCode.Created, table.StatusCode);
        var tableData = await AdminAuthApiClient.ReadDataAsync(table);
        var tableId = tableData.GetProperty("id").GetInt32();
        var tableVersion = tableData.GetProperty("version").GetString();
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Put, $"{Admin}tables/{tableId}", reception, TableBody(areaId, "B1"), $"\"{tableVersion}\"")).StatusCode);

        // Trùng tên trong khu vực; thiếu / sai / đúng If-Match
        Assert.Equal(ErrorCodes.DuplicateName, await ErrorOf(_api.SendAsync(HttpMethod.Post, Admin + "tables", manager, TableBody(areaId, "B1"))));
        Assert.Equal(ErrorCodes.PreconditionRequired, await ErrorOf(_api.SendAsync(HttpMethod.Put, $"{Admin}tables/{tableId}", manager, TableBody(areaId, "B1 cửa sổ"))));
        Assert.Equal(ErrorCodes.PreconditionFailed, await ErrorOf(_api.SendAsync(HttpMethod.Put, $"{Admin}tables/{tableId}", manager, TableBody(areaId, "B1 cửa sổ"), "\"AAAAAAAAAAA\"")));
        var updated = await _api.SendAsync(HttpMethod.Put, $"{Admin}tables/{tableId}", manager, TableBody(areaId, "B1 cửa sổ"), $"\"{tableVersion}\"");
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        // Cùng ETag cũ lần nữa ⇒ người thứ hai bị chặn (BR-TBL-16)
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await _api.SendAsync(HttpMethod.Put, $"{Admin}tables/{tableId}", manager, TableBody(areaId, "B1 góc"), $"\"{tableVersion}\"")).StatusCode);

        // Khu vực còn bàn ⇒ 409 (BR-TBL-14)
        Assert.Equal(ErrorCodes.AreaHasTables, await ErrorOf(_api.SendAsync(HttpMethod.Delete, $"{Admin}table-areas/{areaId}", manager, ifMatch: areaETag)));

        await using var db = _fixture.CreateContext();
        var audit = await db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.Action == "tables.table.updated" && a.EntityId == tableId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Contains("\"B1\"", audit.OldDataJson, StringComparison.Ordinal);
        Assert.Contains("B1 cửa sổ", audit.NewDataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gio_mo_cua_ca_phuc_vu_va_trang_cong_khai()
    {
        await TestSchedule.ResetToSeedAsync(_fixture);
        var superAdmin = await _api.SignInAsync(StaffRole.SuperAdmin);
        var manager = await _api.SignInAsync(StaffRole.Manager);

        // Seed: đủ 7 ngày, đang đóng cửa
        var weekly = await _api.SendAsync(HttpMethod.Get, Admin + "opening-hours", superAdmin);
        var weeklyData = await AdminAuthApiClient.ReadDataAsync(weekly);
        Assert.Equal(7, weeklyData.GetProperty("days").GetArrayLength());
        Assert.All(weeklyData.GetProperty("days").EnumerateArray(), d => Assert.True(d.GetProperty("closed").GetBoolean()));

        // Chỉ SuperAdmin sửa giờ thường kỳ
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Put, Admin + "opening-hours", manager, TwoShiftWeek(), weekly.Headers.ETag?.Tag)).StatusCode);
        var saved = await _api.SendAsync(HttpMethod.Put, Admin + "opening-hours", superAdmin, TwoShiftWeek(), weekly.Headers.ETag?.Tag);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("10:30", (await AdminAuthApiClient.ReadDataAsync(saved)).GetProperty("days")[1].GetProperty("ranges")[0].GetProperty("open").GetString());

        // Ca phải nằm trọn trong một khoảng mở và không chồng lấn
        Assert.Equal(ErrorCodes.SlotOutsideOpeningHours, await ErrorOf(_api.SendAsync(HttpMethod.Post, Admin + "service-slots", manager, Slot("10:00", "15:00"))));
        Assert.Equal(HttpStatusCode.Created, (await _api.SendAsync(HttpMethod.Post, Admin + "service-slots", manager, Slot("11:00", "13:00"))).StatusCode);
        Assert.Equal(ErrorCodes.SlotOverlap, await ErrorOf(_api.SendAsync(HttpMethod.Post, Admin + "service-slots", manager, Slot("12:00", "14:00"))));

        // Thu hẹp giờ mở làm ca đang có lọt ra ngoài ⇒ từ chối
        var current = await _api.SendAsync(HttpMethod.Get, Admin + "opening-hours", superAdmin);
        Assert.Equal(ErrorCodes.SlotOutsideOpeningHours, await ErrorOf(_api.SendAsync(HttpMethod.Put, Admin + "opening-hours", superAdmin, ShortWeek(), current.Headers.ETag?.Tag)));

        // Ngày lễ đóng cửa (Manager làm được) hiện ngay ở trang công khai
        var holiday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(3).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        using var anonymous = _factory.CreateApiClient();
        (await anonymous.GetAsync("/api/v1/public/opening-hours")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Put, $"{Admin}special-hours/{holiday}", manager, new { closed = true, ranges = Array.Empty<object>(), note = "Nghỉ lễ" })).StatusCode);

        var publicHours = await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/public/opening-hours");
        var special = publicHours.GetProperty("data").GetProperty("special").EnumerateArray().Single(s => string.Equals(s.GetProperty("date").GetString(), holiday, StringComparison.Ordinal));
        Assert.True(special.GetProperty("closed").GetBoolean());
    }

    [Fact]
    public async Task Khoa_ngay_khong_nhan_ngay_da_qua_va_xoa_duoc()
    {
        var manager = await _api.SignInAsync(StaffRole.Manager);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var past = await _api.SendAsync(HttpMethod.Post, Admin + "blocked-dates", manager, Blocked(today.AddDays(-1)));
        var created = await _api.SendAsync(HttpMethod.Post, Admin + "blocked-dates", manager, Blocked(today.AddDays(1)));

        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await AdminAuthApiClient.ReadDataAsync(created)).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await _api.SendAsync(HttpMethod.Delete, $"{Admin}blocked-dates/{id}", manager)).StatusCode);
    }

    private static object TableBody(int areaId, string name) =>
        new { areaId, name, minCapacity = 2, maxCapacity = 4, layout = new { x = 10, y = 20, width = 60, height = 60, rotation = 0 }, isActive = true };

    private static object Slot(string start, string end) =>
        new { dayOfWeek = (int?)null, start, end, maxReservations = 10, maxGuests = (int?)40, isActive = true };

    private static object Blocked(DateOnly date) =>
        new { date = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), appliesTo = "reservation", start = "12:00", end = "14:00", reason = "Tiệc riêng" };

    // BR-TBL-16: nhiều lần lưu cùng một ETag cùng lúc ⇒ đúng một lần thành công, các lần còn lại nhận 412 (không 409/500, không ghi chồng)
    [Fact]
    public async Task Luu_gio_mo_cua_dong_thoi_chi_mot_lan_thanh_cong()
    {
        await TestSchedule.ResetToSeedAsync(_fixture);
        var superAdmin = await _api.SignInAsync(StaffRole.SuperAdmin);
        try
        {
            // Có dòng sẵn (seed) và bảng trống: trường hợp bảng trống không có RowVersion nào để phát hiện ghi chồng
            foreach (var empty in new[] { false, true })
            {
                if (empty)
                {
                    await using var clear = _fixture.CreateContext();
                    await clear.OpeningHours.ExecuteDeleteAsync();
                }

                var etag = (await _api.SendAsync(HttpMethod.Get, Admin + "opening-hours", superAdmin)).Headers.ETag?.Tag;
                var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
                    _api.SendAsync(HttpMethod.Put, Admin + "opening-hours", superAdmin, i % 2 == 0 ? TwoShiftWeek() : ShortWeek(), etag)));

                Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
                Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.PreconditionFailed, r.StatusCode));
                await using var db = _fixture.CreateContext();
                var rows = await db.OpeningHours.CountAsync();
                Assert.True(rows is 7 or 14, $"Số dòng giờ mở cửa: {rows}");
            }
        }
        finally
        {
            await TestSchedule.ResetToSeedAsync(_fixture);
        }
    }

    private static object TwoShiftWeek() => Week(new[] { ("10:30", "14:00"), ("17:00", "21:00") });

    private static object ShortWeek() => Week(new[] { ("10:30", "12:00") });

    private static object Week((string Open, string Close)[] ranges) => new
    {
        days = Enumerable.Range(0, 7).Select(d => new
        {
            dayOfWeek = d,
            closed = false,
            ranges = ranges.Select(r => new { open = r.Open, close = r.Close, crossesMidnight = false }).ToArray(),
        }).ToArray(),
    };

    private static async Task<string?> ErrorOf(Task<HttpResponseMessage> request) =>
        await AuthApiClient.ReadErrorCodeAsync(await request);
}
