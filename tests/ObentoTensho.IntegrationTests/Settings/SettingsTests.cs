using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Settings.Domain;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Reservations;

namespace ObentoTensho.IntegrationTests.Settings;

// docs/srs/19 §3, §5: danh mục khóa, kiểm giá trị, khóa thanh toán có kiểm soát
[Collection(SqlServerFixture.CollectionName)]
public sealed class SettingsTests : IDisposable
{
    private const string Path = "/api/v1/admin/settings";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public SettingsTests(SqlServerFixture fixture)
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

    private Task<HttpResponseMessage> PutAsync(string token, Dictionary<string, string> values, string? stepUp = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, Path) { Content = JsonContent.Create(new { values }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (stepUp is not null)
        {
            request.Headers.Add("X-Step-Up-Token", stepUp);
        }

        return _http.SendAsync(request);
    }

    private static Dictionary<string, string> One(string key, string value) => new(StringComparer.Ordinal) { [key] = value };

    private async Task<string> StepUpAsync(string token)
    {
        var response = await _admin.SendAsync(HttpMethod.Post, "/api/v1/admin/auth/step-up", token, new { password = AdminAuthApiClient.Password, code = (string?)null });
        return (await AdminAuthApiClient.ReadDataAsync(response)).GetProperty("token").GetString()!;
    }

    private async Task<string> StoredAsync(string key)
    {
        await using var db = _fixture.CreateContext();
        return await db.SystemSettings.Where(s => s.Key == key).Select(s => s.Value).SingleAsync();
    }

    private async Task<JsonElement> PublicAsync() =>
        await AdminAuthApiClient.ReadDataAsync(await _http.GetAsync("/api/v1/public/settings"));

    [Fact]
    public async Task Moi_khoa_trong_danh_muc_duoc_seed_va_khong_ghi_de_gia_tri_da_sua()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);

        await using var db = _fixture.CreateContext();
        var keys = await db.SystemSettings.Select(s => s.Key).ToListAsync();
        Assert.All(SettingsCatalog.All, d => Assert.Contains(d.Key, keys, StringComparer.Ordinal));
        Assert.Equal("100000", await StoredAsync(SettingKeys.EmailDailyQuota));

        var list = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, Path, manager));
        Assert.Equal(SettingsCatalog.All.Count, list.GetArrayLength());

        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, Path, cashier)).StatusCode);
    }

    [Theory]
    [InlineData("loyalty.pointsPerVnd", "0")]
    [InlineData("reservation.maxGuestsPerBooking", "101")]
    [InlineData("reservation.maxGuestsPerBooking", "abc")]
    [InlineData("reservation.requireContactVerify", "co")]
    [InlineData("social.facebook", "http://facebook.com/quan")]
    [InlineData("otp.channel", "telegram")]
    [InlineData("report.dayCutoffLocal", "07:00")]
    [InlineData("i18n.enabledLocales", "vi,fr")]
    [InlineData("seo.defaultOgImageId", "https://evil.test/a.png")]
    [InlineData("foo.bar", "1")]
    public async Task Gia_tri_sai_hoac_khoa_la_bi_400(string key, string value)
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);

        var response = await PutAsync(manager, One(key, value));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sua_co_audit_va_trang_cong_khai_doc_duoc_ngay()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var address = $"12 Lê Lợi {Guid.NewGuid():N}";

        Assert.Equal(HttpStatusCode.NoContent, (await PutAsync(manager, One(SettingKeys.RestaurantAddress, address))).StatusCode);

        var page = await PublicAsync();
        Assert.Equal(address, page.GetProperty("restaurant.address").GetString());
        Assert.False(page.TryGetProperty("loyalty.pointsPerVnd", out _));
        Assert.False(page.TryGetProperty("email.dailyQuota", out _));

        await using var db = _fixture.CreateContext();
        var audit = await db.AuditLogs.OrderByDescending(a => a.Id).FirstAsync(a => a.Action == "settings.SystemSetting.updated" && a.EntityId == SettingKeys.RestaurantAddress);
        Assert.Contains(address, audit.NewDataJson!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ghi_theo_lo_mot_khoa_sai_thi_khong_ghi_khoa_nao()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var before = await StoredAsync(SettingKeys.ReservationCallAttempts);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SettingKeys.ReservationCallAttempts] = "7",
            [SettingKeys.ReservationGraceMinutes] = "1",
        };

        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(manager, values)).StatusCode);
        Assert.Equal(before, await StoredAsync(SettingKeys.ReservationCallAttempts));

        // Ngưỡng hạng phải tăng dần, xét cùng giá trị đang có
        var tier = await PutAsync(manager, One(SettingKeys.LoyaltyTierGold, "100"));
        Assert.Equal(HttpStatusCode.BadRequest, tier.StatusCode);
        Assert.NotEqual("100", await StoredAsync(SettingKeys.LoyaltyTierGold), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Khoa_thanh_toan_chi_SuperAdmin_co_step_up_va_bao_moi_SuperAdmin()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var superAdmin = await _admin.SignInAsync(StaffRole.SuperAdmin);
        var account = Random.Shared.Next(100_000, 999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var values = One(SettingKeys.PaymentAccountNumber, account);

        Assert.Equal(HttpStatusCode.Forbidden, (await PutAsync(manager, values, await StepUpAsync(manager))).StatusCode);
        var noStepUp = await PutAsync(superAdmin, values);
        Assert.Equal(HttpStatusCode.Unauthorized, noStepUp.StatusCode);
        Assert.Equal(ErrorCodes.StepUpRequired, await AuthApiClient.ReadErrorCodeAsync(noStepUp));
        Assert.NotEqual(account, await StoredAsync(SettingKeys.PaymentAccountNumber), StringComparer.Ordinal);

        Assert.Equal(HttpStatusCode.Forbidden, (await PutAsync(manager, One(SettingKeys.RestaurantPhone, "0901234567"))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await PutAsync(superAdmin, values, await StepUpAsync(superAdmin))).StatusCode);
        Assert.Equal(account, await StoredAsync(SettingKeys.PaymentAccountNumber));

        await using (var db = _fixture.CreateContext())
        {
            Assert.True(await db.OutboxMessages.AnyAsync(m => m.IdempotencyKey.StartsWith("admin_alert:settings:") && m.PayloadJson.Contains("payment.accountNumber")));
        }

        // QR chỉ nhận ảnh đã có trong pipeline
        var qr = await PutAsync(superAdmin, One(SettingKeys.PaymentQrImageId, Guid.NewGuid().ToString()), await StepUpAsync(superAdmin));
        Assert.Equal(HttpStatusCode.BadRequest, qr.StatusCode);
    }

    [Fact]
    public async Task Doi_gia_tri_co_hieu_luc_voi_nghiep_vu_ngay()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var settings = _factory.Services.GetRequiredService<ILoyaltySettings>();
        var before = await StoredAsync(SettingKeys.LoyaltyRedeemRequiredPoints);

        Assert.Equal(HttpStatusCode.NoContent, (await PutAsync(manager, One(SettingKeys.LoyaltyRedeemRequiredPoints, "222"))).StatusCode);
        Assert.Equal(222, settings.RedeemRequiredPoints);

        Assert.Equal(HttpStatusCode.NoContent, (await PutAsync(manager, One(SettingKeys.LoyaltyRedeemRequiredPoints, before))).StatusCode);
    }

    [Fact]
    public async Task Han_huy_dat_ban_mac_dinh_tu_do_va_co_the_bat()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var api = new ReservationApiClient(_http);
        var email = AuthApiClient.NewEmail();
        var code = await ReservationApiClient.CodeOf(await api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow, email: email)));
        await _factory.ProcessOutboxAsync();
        var token = ReservationApiClient.TokenIn(_factory.Emails, email);

        Assert.Equal(HttpStatusCode.NoContent, (await PutAsync(manager, One(SettingKeys.ReservationCancellationHours, "72"))).StatusCode);
        try
        {
            Assert.Equal(ErrorCodes.InvalidTransition, await AuthApiClient.ReadErrorCodeAsync(await api.CancelAsync(code, token)));
        }
        finally
        {
            await PutAsync(manager, One(SettingKeys.ReservationCancellationHours, "0"));
        }

        Assert.Equal(HttpStatusCode.OK, (await api.CancelAsync(code, token)).StatusCode);
    }

    [Fact]
    public async Task Gui_thu_thu_vao_hop_thu_nguoi_bam()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);

        Assert.Equal(HttpStatusCode.Accepted, (await _admin.SendAsync(HttpMethod.Post, $"{Path}/test-email", manager)).StatusCode);
    }
}
