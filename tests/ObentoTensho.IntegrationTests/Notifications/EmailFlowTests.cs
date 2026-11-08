using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Core.Notifications.Services;
using ObentoTensho.Core.Notifications.Domain;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Notifications;

// docs/srs/20 §6 và FR-AUTH-05/07: email qua hộp thư đi, xác thực email, quên/đặt lại mật khẩu, giới hạn, thử lại
[Collection(SqlServerFixture.CollectionName)]
public sealed partial class EmailFlowTests : IDisposable
{
    private const string NewPassword = "Mat-khau-moi-sau-dat-lai";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AuthApiClient _customer;

    public EmailFlowTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _customer = new AuthApiClient(_http);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Dang_ky_gui_thu_xac_thuc_va_lien_ket_chi_dung_mot_lan()
    {
        var email = AuthApiClient.NewEmail();
        (await _customer.RegisterAsync(email)).EnsureSuccessStatusCode();

        await _factory.ProcessOutboxAsync();

        var mail = Assert.Single(_factory.Emails.To(email));
        Assert.Equal("Xác nhận email tài khoản Obento Tensho", mail.Subject);
        Assert.Contains($"{ApiFactory.CustomerOrigin}/auth/verify-email#token=", mail.TextBody, StringComparison.Ordinal);
        var token = TokenIn(mail.TextBody);

        Assert.Equal(HttpStatusCode.NoContent, (await Post("/api/v1/auth/verify-email", new { token })).StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await AuthApiClient.ReadErrorCodeAsync(await Post("/api/v1/auth/verify-email", new { token })));

        await using var db = _fixture.CreateContext();
        Assert.True(await db.Users.Where(u => u.Email == email).Select(u => u.IsEmailVerified).SingleAsync());
    }

    [Fact]
    public async Task Quen_mat_khau_phan_hoi_trung_tinh_dat_lai_thu_hoi_phien_va_bao_chu_tai_khoan()
    {
        var email = AuthApiClient.NewEmail();
        var (_, oldRefresh) = await _customer.RegisterAndLoginAsync(email);

        var known = await Post("/api/v1/auth/forgot-password", new { email });
        var unknown = await Post("/api/v1/auth/forgot-password", new { email = AuthApiClient.NewEmail() });
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);

        await _factory.ProcessOutboxAsync();
        var token = TokenIn(_factory.Emails.To(email).Single(m => m.Subject.StartsWith("Đặt lại mật khẩu", StringComparison.Ordinal)).TextBody);

        Assert.Equal(HttpStatusCode.NoContent, (await Post("/api/v1/auth/reset-password", new { token, newPassword = NewPassword })).StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await AuthApiClient.ReadErrorCodeAsync(await Post("/api/v1/auth/reset-password", new { token, newPassword = NewPassword + "-2" })));
        Assert.Equal(HttpStatusCode.Unauthorized, (await _customer.RefreshAsync(oldRefresh)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _customer.LoginAsync(email)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _customer.LoginAsync(email, NewPassword)).StatusCode);

        await _factory.ProcessOutboxAsync();
        Assert.Contains(_factory.Emails.To(email), m => m.Subject.Contains("vừa được thay đổi", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Nhan_vien_quen_mat_khau_nhan_lien_ket_trang_quan_tri()
    {
        var admin = new AdminAuthApiClient(_factory, _fixture);
        var (_, email) = await admin.CreateStaffAsync(StaffRole.Reception);

        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsync("forgot-password", new { email })).StatusCode);
        await _factory.ProcessOutboxAsync();

        var mail = Assert.Single(_factory.Emails.To(email));
        Assert.Contains($"{ApiFactory.AdminOrigin}/auth/reset-password#token=", mail.TextBody, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("reset-password", new { token = TokenIn(mail.TextBody), newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.LoginAsync(email, NewPassword)).StatusCode);
    }

    // Q1: thư giao dịch cho chính chủ không bị giới hạn 3 thư/giờ
    [Fact]
    public async Task Thu_giao_dich_khong_bi_gioi_han_3_thu_moi_gio()
    {
        var email = AuthApiClient.NewEmail();
        (await _customer.RegisterAsync(email)).EnsureSuccessStatusCode();
        for (var i = 0; i < 4; i++)
        {
            (await Post("/api/v1/auth/forgot-password", new { email })).EnsureSuccessStatusCode();
            await _factory.ProcessOutboxAsync();
        }

        Assert.Equal(4, _factory.Emails.To(email).Count(m => m.Subject.StartsWith("Đặt lại mật khẩu", StringComparison.Ordinal)));
        await using var db = _fixture.CreateContext();
        Assert.Equal(0, await db.EmailLogs.CountAsync(l => l.ToEmail == email && l.Status == EmailStatus.Suppressed));
    }

    // Q1: vượt giới hạn theo địa chỉ ⇒ thư quảng bá dời sang ngày sau (không ghi bỏ), thư giao dịch quá 30/ngày thì bỏ, thư nội bộ vẫn gửi
    [Fact]
    public async Task Vuot_gioi_han_thi_quang_ba_doi_giao_dich_bo_noi_bo_van_gui()
    {
        var email = AuthApiClient.NewEmail();
        await using (var db = _fixture.CreateContext())
        {
            var now = DateTime.UtcNow;
            db.EmailLogs.AddRange(Enumerable.Range(0, EmailPolicy.MaxTransactionalPerDay).Select(i => new EmailLog
            {
                Type = "order_received",
                ToEmail = email,
                Status = EmailStatus.Sent,
                SentAtUtc = now,
                CreatedAtUtc = now,
                IdempotencyKey = $"seed:{email}:{i}",
            }));
            await db.SaveChangesAsync();
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatchService>();
        EmailRequest Request(EmailKind kind) => new(kind, email, "vi", new Dictionary<string, string>(StringComparer.Ordinal) { ["message"] = "x", ["adminLink"] = "https://admin.test", ["code"] = "123456" }, $"{kind}:{email}", RelatedEntityId: null);

        Assert.Equal(DispatchOutcome.Deferred, await dispatcher.DispatchAsync(Request(EmailKind.LoyaltyTierUp), CancellationToken.None));
        Assert.Equal(DispatchOutcome.Suppressed, await dispatcher.DispatchAsync(Request(EmailKind.VerificationCode), CancellationToken.None));
        Assert.Equal(DispatchOutcome.Sent, await dispatcher.DispatchAsync(Request(EmailKind.AdminAlert), CancellationToken.None));

        await using var check = _fixture.CreateContext();
        Assert.False(await check.EmailLogs.AnyAsync(l => l.IdempotencyKey == $"{EmailKind.LoyaltyTierUp}:{email}"));
    }

    [Fact]
    public async Task Loi_SMTP_thu_lai_5_lan_roi_vao_hang_that_bai_va_xoa_token_khoi_payload()
    {
        var email = AuthApiClient.NewEmail();
        (await _customer.RegisterAsync(email)).EnsureSuccessStatusCode();
        _factory.Emails.Fail = true;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await _factory.ProcessOutboxAsync();

            // Không chờ thật: đưa thời điểm thử lại về hiện tại
            await using var db = _fixture.CreateContext();
            await db.OutboxMessages.Where(m => m.PayloadJson.Contains(email))
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.NextAttemptAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        }

        await using var check = _fixture.CreateContext();
        var message = await check.OutboxMessages.AsNoTracking()
            .Where(m => m.FailedAtUtc != null && m.Attempts == 5)
            .OrderByDescending(m => m.Id)
            .FirstAsync();
        Assert.Equal("{}", message.PayloadJson);
        Assert.Equal(EmailStatus.Failed, (await check.EmailLogs.SingleAsync(l => l.ToEmail == email)).Status);
        Assert.Empty(_factory.Emails.To(email));
    }

    [Fact]
    public async Task Ten_nguoi_dung_duoc_escape_trong_HTML()
    {
        var email = AuthApiClient.NewEmail();
        var response = await _http.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "<b>An</b>",
            email,
            phoneNumber = "0912345678",
            password = AuthApiClient.DefaultPassword,
            dateOfBirth = (string?)null,
            preferredLang = "ja",
        });
        response.EnsureSuccessStatusCode();

        await _factory.ProcessOutboxAsync();

        var mail = Assert.Single(_factory.Emails.To(email));
        Assert.Contains("&lt;b&gt;An&lt;/b&gt; 様", mail.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>An</b>", mail.HtmlBody, StringComparison.Ordinal);
        Assert.StartsWith("<b>An</b> 様", mail.TextBody, StringComparison.Ordinal);
        Assert.Equal("Obento Tensho アカウントのメールアドレス確認", mail.Subject);
    }

    private Task<HttpResponseMessage> Post(string path, object body) => _http.PostAsJsonAsync(path, body);

    private static string TokenIn(string body) =>
        Uri.UnescapeDataString(TokenPattern().Match(body) is { Success: true } m ? m.Groups["token"].Value : throw new InvalidOperationException("Không thấy token."));

    [GeneratedRegex(@"#token=(?<token>[A-Za-z0-9_%\-]+)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TokenPattern();
}
