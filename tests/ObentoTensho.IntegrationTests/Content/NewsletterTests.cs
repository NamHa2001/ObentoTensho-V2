using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Content.Services;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Content;

// docs/srs/17 §3.6, BR-CNT-20..26: đăng ký hai bước, hủy bằng POST, gửi theo lô
[Collection(SqlServerFixture.CollectionName)]
public sealed class NewsletterTests : IDisposable
{
    private const string Public = "/api/v1/newsletter";
    private const string Admin = "/api/v1/admin/newsletter";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public NewsletterTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string>(StringComparer.Ordinal) { ["Email:DailyQuota"] = "100000" });
        _http = _factory.CreateApiClient();
        _admin = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> SubscribeAsync(string email, string? website = null) =>
        _http.PostAsJsonAsync($"{Public}/subscribe", new { email, lang = "en", website });

    private Task<HttpResponseMessage> ConfirmAsync(string token) =>
        _http.PostAsJsonAsync($"{Public}/confirm", new { token });

    private Task<HttpResponseMessage> UnsubscribeAsync(string token) =>
        _http.PostAsJsonAsync($"{Public}/unsubscribe", new { token });

    // Liên kết xác nhận mới nhất đang chờ gửi cho địa chỉ này
    private async Task<string> ConfirmTokenAsync(string email)
    {
        await using var db = _fixture.CreateContext();
        var payload = await db.OutboxMessages
            .Where(m => m.IdempotencyKey.StartsWith("newsletter_confirm:") && m.PayloadJson.Contains(email))
            .OrderByDescending(m => m.Id)
            .Select(m => m.PayloadJson)
            .FirstAsync();
        var link = JsonDocument.Parse(payload).RootElement.GetProperty("values").GetProperty("link").GetString()!;
        return Uri.UnescapeDataString(link[(link.IndexOf("#token=", StringComparison.Ordinal) + 7)..]);
    }

    private async Task<NewsletterSubscriber> SubscriberAsync(string email)
    {
        await using var db = _fixture.CreateContext();
        return await db.NewsletterSubscribers.AsNoTracking().SingleAsync(s => s.Email == email);
    }

    private async Task<string> ConfirmedAsync()
    {
        var email = AuthApiClient.NewEmail();
        Assert.Equal(HttpStatusCode.OK, (await SubscribeAsync(email)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmAsync(await ConfirmTokenAsync(email))).StatusCode);
        return email;
    }

    private string UnsubscribeToken(NewsletterSubscriber subscriber)
    {
        var protector = _factory.Services.GetRequiredService<INewsletterTokenProtector>();
        return protector.Unprotect(subscriber.UnsubscribeTokenProtected)!;
    }

    private async Task<JsonElement> CreateCampaignAsync(string token, string subject, string html = "<p>Tin mới</p><script>alert(1)</script>", DateTime? scheduledAtUtc = null)
    {
        var response = await _admin.SendAsync(HttpMethod.Post, $"{Admin}/campaigns", token, new { subject, contentHtml = html, scheduledAtUtc });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await AdminAuthApiClient.ReadDataAsync(response);
    }

    private Task<HttpResponseMessage> SendCampaignAsync(string token, JsonElement campaign) =>
        _admin.SendAsync(HttpMethod.Post, $"{Admin}/campaigns/{campaign.GetProperty("id").GetInt32()}/send", token, null, $"\"{campaign.GetProperty("version").GetString()}\"");

    private async Task<int> RunJobAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NewsletterSendJob>().RunAsync(CancellationToken.None);
    }

    private async Task RunUntilFinishedAsync(int campaignId)
    {
        for (var i = 0; i < 200; i++)
        {
            await using var db = _fixture.CreateContext();
            var status = await db.NewsletterCampaigns.Where(c => c.Id == campaignId).Select(c => c.Status).SingleAsync();
            if (status == NewsletterCampaignStatus.Finished)
            {
                return;
            }

            await RunJobAsync();
        }

        Assert.Fail("Chiến dịch không kết thúc.");
    }

    private async Task<NewsletterRecipient?> RecipientAsync(int campaignId, int subscriberId)
    {
        await using var db = _fixture.CreateContext();
        return await db.NewsletterRecipients.AsNoTracking().SingleOrDefaultAsync(r => r.CampaignId == campaignId && r.SubscriberId == subscriberId);
    }

    [Fact]
    public async Task Dang_ky_hai_buoc_trung_thi_bao_da_dang_ky_va_dang_ky_lai_sau_huy()
    {
        var email = AuthApiClient.NewEmail();

        var first = await AdminAuthApiClient.ReadDataAsync(await SubscribeAsync(email));
        Assert.Equal("pending_confirmation", first.GetProperty("status").GetString());
        Assert.False((await SubscriberAsync(email)).IsActive);
        Assert.Equal(HttpStatusCode.OK, (await SubscribeAsync($"bay.{email}", "https://spam.test")).StatusCode);

        var token = await ConfirmTokenAsync(email);
        var viaGet = await _http.GetAsync($"{Public}/confirm?token={Uri.EscapeDataString(token)}");
        Assert.NotEqual(HttpStatusCode.OK, viaGet.StatusCode);
        Assert.False((await SubscriberAsync(email)).IsActive);

        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmAsync(token)).StatusCode);
        var subscriber = await SubscriberAsync(email);
        Assert.True(subscriber.IsSubscribed);
        Assert.Equal(ErrorCodes.TokenInvalid, await AuthApiClient.ReadErrorCodeAsync(await ConfirmAsync(token)));

        await using (var db = _fixture.CreateContext())
        {
            Assert.False(await db.NewsletterSubscribers.AnyAsync(s => s.Email == $"bay.{email}"));
            Assert.Equal(1, await db.ConsentRecords.CountAsync(c => c.Email == email && c.DocType == ConsentRecord.Marketing));
        }

        var again = await AdminAuthApiClient.ReadDataAsync(await SubscribeAsync(email));
        Assert.Equal("already_subscribed", again.GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await UnsubscribeAsync(UnsubscribeToken(subscriber))).StatusCode);
        Assert.False((await SubscriberAsync(email)).IsActive);

        // Đăng ký lại phải xác nhận lại mới được nhận thư
        Assert.Equal("pending_confirmation", (await AdminAuthApiClient.ReadDataAsync(await SubscribeAsync(email))).GetProperty("status").GetString());
        Assert.False((await SubscriberAsync(email)).IsSubscribed);
        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmAsync(await ConfirmTokenAsync(email))).StatusCode);
        var back = await SubscriberAsync(email);
        Assert.True(back.IsSubscribed);
        Assert.Null(back.UnsubscribedAtUtc);
    }

    [Fact]
    public async Task Lien_ket_xac_nhan_het_han_va_gioi_han_theo_email()
    {
        var email = AuthApiClient.NewEmail();
        await SubscribeAsync(email);
        var token = await ConfirmTokenAsync(email);
        await using (var db = _fixture.CreateContext())
        {
            var past = DateTime.UtcNow.AddMinutes(-1);
            await db.NewsletterSubscribers.Where(s => s.Email == email).ExecuteUpdateAsync(s => s.SetProperty(x => x.ConfirmExpiryUtc, past));
        }

        Assert.Equal(ErrorCodes.TokenInvalid, await AuthApiClient.ReadErrorCodeAsync(await ConfirmAsync(token)));
        Assert.False((await SubscriberAsync(email)).IsActive);

        await SubscribeAsync(email);
        await SubscribeAsync(email);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SubscribeAsync(email)).StatusCode);
    }

    [Fact]
    public async Task Huy_chi_bang_POST_va_one_click_theo_RFC_8058()
    {
        var email = await ConfirmedAsync();
        var token = UnsubscribeToken(await SubscriberAsync(email));

        Assert.NotEqual(HttpStatusCode.NoContent, (await _http.GetAsync($"{Public}/one-click?token={Uri.EscapeDataString(token)}")).StatusCode);
        Assert.NotEqual(HttpStatusCode.NoContent, (await _http.GetAsync($"{Public}/unsubscribe?token={Uri.EscapeDataString(token)}")).StatusCode);
        Assert.True((await SubscriberAsync(email)).IsActive);

        Assert.Equal(ErrorCodes.TokenInvalid, await AuthApiClient.ReadErrorCodeAsync(await UnsubscribeAsync("sai")));

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal) { ["List-Unsubscribe"] = "One-Click" });
        Assert.Equal(HttpStatusCode.NoContent, (await _http.PostAsync($"{Public}/one-click?token={Uri.EscapeDataString(token)}", form)).StatusCode);
        Assert.False((await SubscriberAsync(email)).IsActive);

        // Bấm lại không lỗi
        Assert.Equal(HttpStatusCode.NoContent, (await UnsubscribeAsync(token)).StatusCode);
    }

    [Fact]
    public async Task Gui_chien_dich_qua_job_chi_toi_nguoi_da_xac_nhan_va_khong_gui_lai()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var (reader, unconfirmed) = (await ConfirmedAsync(), AuthApiClient.NewEmail());
        await SubscribeAsync(unconfirmed);

        var campaign = await CreateCampaignAsync(manager, "Ưu đãi tháng 11");
        var id = campaign.GetProperty("id").GetInt32();
        Assert.DoesNotContain("script", campaign.GetProperty("contentHtml").GetString()!, StringComparison.Ordinal);

        // Bấm gửi không gửi gì trong request
        Assert.Equal(HttpStatusCode.NoContent, (await SendCampaignAsync(manager, campaign)).StatusCode);
        Assert.Empty(_factory.Emails.To(reader));

        await RunUntilFinishedAsync(id);

        var mail = Assert.Single(_factory.Emails.To(reader));
        Assert.Equal("Ưu đãi tháng 11", mail.Subject);
        Assert.Contains("<p>Tin mới</p>", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("/newsletter/unsubscribe#token=", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("/newsletter/unsubscribe#token=", mail.TextBody, StringComparison.Ordinal);
        Assert.Contains("Tin mới", mail.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<p>", mail.TextBody, StringComparison.Ordinal);
        Assert.Equal("List-Unsubscribe=One-Click", mail.Headers!["List-Unsubscribe-Post"]);
        Assert.Contains("/api/v1/newsletter/one-click?token=", mail.Headers["List-Unsubscribe"], StringComparison.Ordinal);
        Assert.Empty(_factory.Emails.To(unconfirmed));

        var subscriber = await SubscriberAsync(reader);
        var recipient = await RecipientAsync(id, subscriber.Id);
        Assert.Equal(NewsletterRecipientStatus.Sent, recipient?.Status);
        Assert.Null(await RecipientAsync(id, (await SubscriberAsync(unconfirmed)).Id));

        var detail = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/campaigns/{id}", manager));
        Assert.Equal("finished", detail.GetProperty("status").GetString());
        Assert.Equal(detail.GetProperty("recipientCount").GetInt32(), detail.GetProperty("sentCount").GetInt32());

        await RunJobAsync();
        Assert.Single(_factory.Emails.To(reader));
    }

    [Fact]
    public async Task Huy_sau_khi_chot_danh_sach_thi_khong_nhan_va_loi_SMTP_thu_lai_roi_ghi_loi()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var (leaver, unlucky) = (await ConfirmedAsync(), await ConfirmedAsync());
        var campaign = await CreateCampaignAsync(manager, "Thư có lỗi");
        var id = campaign.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await SendCampaignAsync(manager, campaign)).StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<INewsletterRepository>().SnapshotRecipientsAsync(id, CancellationToken.None);
        }

        var gone = await SubscriberAsync(leaver);
        Assert.Equal(HttpStatusCode.NoContent, (await UnsubscribeAsync(UnsubscribeToken(gone))).StatusCode);

        _factory.Emails.Fail = true;
        await RunUntilFinishedAsync(id);
        _factory.Emails.Fail = false;

        Assert.Empty(_factory.Emails.To(leaver));
        Assert.Equal("unsubscribed", (await RecipientAsync(id, gone.Id))?.Error);
        var failed = await RecipientAsync(id, (await SubscriberAsync(unlucky)).Id);
        Assert.Equal((NewsletterRecipientStatus.Failed, "send_failed", (byte)NewsletterRecipient.MaxAttempts), (failed?.Status, failed?.Error, failed?.Attempts));
    }

    [Fact]
    public async Task Hen_gio_chi_ban_nhap_moi_sua_duoc_va_xuat_danh_sach_co_quyen()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var editor = await _admin.SignInAsync(StaffRole.ContentEditor);

        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, $"{Admin}/campaigns", editor)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, $"{Admin}/subscribers/export", editor)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/campaigns", manager, new { subject = new string('x', 151), contentHtml = "x", scheduledAtUtc = (DateTime?)null })).StatusCode);

        var later = await CreateCampaignAsync(manager, "Tuần sau", scheduledAtUtc: DateTime.UtcNow.AddDays(7));
        var id = later.GetProperty("id").GetInt32();
        var path = $"{Admin}/campaigns/{id}";
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await _admin.SendAsync(HttpMethod.Post, $"{path}/send", manager)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendCampaignAsync(manager, later)).StatusCode);

        await RunJobAsync();
        var current = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, path, manager));
        Assert.Equal("scheduled", current.GetProperty("status").GetString());
        var etag = $"\"{current.GetProperty("version").GetString()}\"";
        Assert.Equal(HttpStatusCode.Conflict, (await _admin.SendAsync(HttpMethod.Put, path, manager, new { subject = "Sửa", contentHtml = "x", scheduledAtUtc = (DateTime?)null }, etag)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _admin.SendAsync(HttpMethod.Delete, path, manager, null, etag)).StatusCode);

        var draft = await CreateCampaignAsync(manager, "Nháp");
        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Delete, $"{Admin}/campaigns/{draft.GetProperty("id").GetInt32()}", manager, null, $"\"{draft.GetProperty("version").GetString()}\"")).StatusCode);

        var export = await _admin.SendAsync(HttpMethod.Get, $"{Admin}/subscribers/export", manager);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", export.Content.Headers.ContentType?.MediaType);
        await using var db = _fixture.CreateContext();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "content.newsletter_subscriber.exported"));
    }
}
