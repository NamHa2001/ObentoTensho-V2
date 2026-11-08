using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Reservations.Services;
using ObentoTensho.Infrastructure.Security;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Reservations;

// BR-RSV-21/22 (OTP chọn lọc), SEC-22/57 (Turnstile), BR-RSV-08/24/25 (việc định kỳ), tệp lịch .ics
[Collection(SqlServerFixture.CollectionName)]
public sealed partial class ReservationVerificationTests : IDisposable
{
    private const string FlaggedPhone = "0913777666";
    private const string FlaggedPhoneNormalized = "+84913777666";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly ReservationApiClient _api;

    public ReservationVerificationTests(SqlServerFixture fixture)
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
    public async Task SDT_bi_gan_co_chi_giu_cho_khi_nhap_dung_ma_OTP_gui_qua_email()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        await FlagPhoneAsync();
        var email = AuthApiClient.NewEmail();
        var booking = ReservationApiClient.Booking(TestSchedule.Tomorrow.AddDays(5), phone: FlaggedPhone, email: email);

        Assert.Equal(ErrorCodes.OtpRequired, await ErrorOf(await _api.CreateAsync(booking)));
        Assert.Equal(HttpStatusCode.Accepted, (await SendOtpAsync(email)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SendOtpAsync(email)).StatusCode);

        await _factory.ProcessOutboxAsync();
        var code = CodePattern().Match(_factory.Emails.To(email).Single().TextBody).Value;

        Assert.Equal(ErrorCodes.OtpInvalid, await ErrorOf(await _api.CreateAsync(WithOtp(booking, string.Equals(code, "000000", StringComparison.Ordinal) ? "111111" : "000000"))));
        Assert.Equal(HttpStatusCode.Created, (await _api.CreateAsync(WithOtp(booking, code))).StatusCode);

        await using var db = _fixture.CreateContext();
        var created = await db.Reservations.SingleAsync(r => r.GuestEmail == email);
        Assert.True(created.OtpRequired);
        Assert.NotNull(created.ContactVerifiedAtUtc);
    }

    [Fact]
    public async Task Sai_OTP_5_lan_thi_ma_bi_huy_ke_ca_nhap_dung_sau_do()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        await FlagPhoneAsync();
        var email = AuthApiClient.NewEmail();
        var booking = ReservationApiClient.Booking(TestSchedule.Tomorrow.AddDays(5), phone: FlaggedPhone, email: email);
        (await SendOtpAsync(email)).EnsureSuccessStatusCode();
        await _factory.ProcessOutboxAsync();
        var code = CodePattern().Match(_factory.Emails.To(email).Single().TextBody).Value;
        var wrong = string.Equals(code, "123456", StringComparison.Ordinal) ? "654321" : "123456";

        for (var i = 0; i < 5; i++)
        {
            await _api.CreateAsync(WithOtp(booking, wrong));
        }

        Assert.Equal(ErrorCodes.OtpInvalid, await ErrorOf(await _api.CreateAsync(WithOtp(booking, code))));
    }

    [Fact]
    public async Task Viec_dinh_ky_nhac_mot_lan_tu_no_show_va_tu_huy_dat_ban_treo()
    {
        var slotId = await TestSchedule.OpenEveryDayAsync(_fixture);
        var remind = await CreateAsync(ReservationStatus.Confirmed, TimeSpan.FromMinutes(120));
        var late = await CreateAsync(ReservationStatus.Confirmed, TimeSpan.FromMinutes(-30));
        var stale = await CreateAsync(ReservationStatus.Pending, TimeSpan.FromMinutes(30));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var jobs = scope.ServiceProvider.GetRequiredService<ReservationJobs>();
            Assert.Equal(1, await jobs.SendRemindersAsync(CancellationToken.None));
            Assert.Equal(0, await jobs.SendRemindersAsync(CancellationToken.None));
            Assert.Equal(1, await jobs.MarkNoShowsAsync(CancellationToken.None));
            Assert.Equal(1, await jobs.CancelStalePendingAsync(CancellationToken.None));
        }

        await using var db = _fixture.CreateContext();
        Assert.NotNull((await db.Reservations.SingleAsync(r => r.Id == remind)).ReminderSentAtUtc);
        Assert.Equal(ReservationStatus.NoShow, (await db.Reservations.SingleAsync(r => r.Id == late)).Status);
        var cancelled = await db.Reservations.SingleAsync(r => r.Id == stale);
        Assert.Equal(ReservationStatus.Cancelled, cancelled.Status);
        Assert.Equal(ReservationActor.System, cancelled.CancelledByType);
        Assert.Equal(1, await db.OutboxMessages.CountAsync(m => m.PayloadJson.Contains("reservation_reminder") && m.PayloadJson.Contains(remind.ToString())));
        Assert.True(await db.OutboxMessages.AnyAsync(m => m.PayloadJson.Contains("reservation_auto_cancelled") && m.PayloadJson.Contains(stale.ToString())));
        Assert.Equal(1, await db.SlotDayCapacity.Where(c => c.SlotId == slotId).SumAsync(c => c.BookedCount));
    }

    [Fact]
    public async Task Thu_da_nhan_dat_ban_co_tep_lich_ics()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var email = AuthApiClient.NewEmail();
        (await _api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow, email: email))).EnsureSuccessStatusCode();

        await _factory.ProcessOutboxAsync();

        var attachment = Assert.Single(_factory.Emails.To(email).Single().Attachments ?? []);
        Assert.Equal("obento-tensho.ics", attachment.FileName);
        Assert.StartsWith("BEGIN:VCALENDAR\r\n", attachment.Content, StringComparison.Ordinal);
        Assert.Contains("DTSTART:", attachment.Content, StringComparison.Ordinal);
    }

    // Tạo đặt bàn rồi dời giờ hẹn về "bây giờ + offset" để việc định kỳ có việc làm mà không cần chờ thật
    private async Task<Guid> CreateAsync(ReservationStatus status, TimeSpan offset)
    {
        var code = await ReservationApiClient.CodeOf(await _api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow)));
        await using var db = _fixture.CreateContext();
        await db.Reservations.Where(r => r.ReservationCode == code).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.Status, status)
            .SetProperty(r => r.ReservedAtUtc, DateTime.UtcNow + offset));
        return await db.Reservations.Where(r => r.ReservationCode == code).Select(r => r.Id).SingleAsync();
    }

    private async Task FlagPhoneAsync()
    {
        for (var i = 0; i < 2; i++)
        {
            (await _api.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow.AddDays(i), phone: FlaggedPhone))).EnsureSuccessStatusCode();
        }

        await using var db = _fixture.CreateContext();
        await db.Reservations.Where(r => r.GuestPhone == FlaggedPhoneNormalized)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReservationStatus.NoShow));
    }

    private Task<HttpResponseMessage> SendOtpAsync(string email) =>
        _http.PostAsJsonAsync("/api/v1/otp/send", new { purpose = "reservation_contact", email, lang = "vi" });

    private static System.Text.Json.Nodes.JsonObject WithOtp(object booking, string code)
    {
        var json = System.Text.Json.JsonSerializer.SerializeToNode(booking)?.AsObject() ?? [];
        json["otpCode"] = code;
        return json;
    }

    private static Task<string?> ErrorOf(HttpResponseMessage response) => AuthApiClient.ReadErrorCodeAsync(response);

    [GeneratedRegex(@"\b\d{6}\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CodePattern();
}
