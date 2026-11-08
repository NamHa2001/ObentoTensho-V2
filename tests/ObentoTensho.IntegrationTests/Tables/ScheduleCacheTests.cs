using System.Globalization;
using System.Net;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Reservations;

namespace ObentoTensho.IntegrationTests.Tables;

// Q13: lịch được cache cho đường đọc; sửa lịch qua trang quản trị thì chỗ trống đổi ngay, không chờ hết hạn cache
[Collection(SqlServerFixture.CollectionName)]
public sealed class ScheduleCacheTests : IDisposable
{
    private const string Admin = "/api/v1/admin/";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _api;

    public ScheduleCacheTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string>(StringComparer.Ordinal) { ["Caching:Schedule:Enabled"] = "true" });
        _http = _factory.CreateApiClient();
        _api = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    private async Task<int> TimesAsync() =>
        (await new ReservationApiClient(_http).AvailabilityAsync(TestSchedule.Tomorrow)).GetProperty("slots")[0].GetProperty("times").GetArrayLength();

    [Fact]
    public async Task Khoa_va_mo_ngay_qua_quan_tri_thi_cho_trong_doi_ngay()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var manager = await _api.SignInAsync(StaffRole.Manager);
        var all = await TimesAsync();

        var created = await _api.SendAsync(HttpMethod.Post, Admin + "blocked-dates", manager, new
        {
            date = TestSchedule.Tomorrow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), appliesTo = "reservation", start = "12:00", end = "14:00", reason = "Tiệc riêng",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(await TimesAsync() < all);

        var id = (await AdminAuthApiClient.ReadDataAsync(created)).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await _api.SendAsync(HttpMethod.Delete, $"{Admin}blocked-dates/{id}", manager)).StatusCode);
        Assert.Equal(all, await TimesAsync());
    }
}
