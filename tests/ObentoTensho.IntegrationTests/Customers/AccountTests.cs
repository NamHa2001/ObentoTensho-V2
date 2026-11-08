using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Customers.Services;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;
using ObentoTensho.IntegrationTests.Reservations;

namespace ObentoTensho.IntegrationTests.Customers;

// docs/srs/10 §4.6–§4.9, BR-AUTH-15, BR-LOY-20, BR-CUS-05: tài khoản khách
[Collection(SqlServerFixture.CollectionName)]
public sealed class AccountTests : IDisposable
{
    private const string Account = "/api/v1/account";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AuthApiClient _auth;
    private readonly AdminAuthApiClient _admin;

    public AccountTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _auth = new AuthApiClient(_http);
        _admin = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    private static object Profile(string name = "Nguyễn Văn An", string phone = "0912 345 678", DateOnly? dob = null, string? lang = null) =>
        new { fullName = name, phoneNumber = phone, dateOfBirth = dob, preferredLang = lang };

    private async Task<(Guid Id, string Email, string Access, string Refresh)> CustomerAsync()
    {
        var email = AuthApiClient.NewEmail();
        var (access, refresh) = await _auth.RegisterAndLoginAsync(email);
        await using var db = _fixture.CreateContext();
        var id = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        return (id, email, access, refresh);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, $"{Account}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _http.SendAsync(request);
    }

    private static DateOnly Today => TestSchedule.TodayVn;

    private async Task<string> EmailTokenAsync(string prefix, string email)
    {
        await using var db = _fixture.CreateContext();
        var payload = await db.OutboxMessages
            .Where(m => m.IdempotencyKey.StartsWith(prefix) && m.PayloadJson.Contains(email))
            .OrderByDescending(m => m.Id)
            .Select(m => m.PayloadJson)
            .FirstAsync();
        var link = JsonDocument.Parse(payload).RootElement.GetProperty("values").GetProperty("link").GetString()!;
        return Uri.UnescapeDataString(link[(link.IndexOf("#token=", StringComparison.Ordinal) + 7)..]);
    }

    [Fact]
    public async Task Ho_so_doi_SDT_chuan_hoa_va_dat_lai_xac_minh()
    {
        var (id, email, access, _) = await CustomerAsync();
        await using (var db = _fixture.CreateContext())
        {
            await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsPhoneVerified, true));
        }

        var profile = await AdminAuthApiClient.ReadDataAsync(await SendAsync(HttpMethod.Get, "/profile", access));
        Assert.Equal(email, profile.GetProperty("email").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Put, "/profile", access, Profile(phone: "123"))).StatusCode);
        var updated = await AdminAuthApiClient.ReadDataAsync(await SendAsync(HttpMethod.Put, "/profile", access, Profile("Tên Mới", "0987 654 321", lang: "ja")));
        Assert.Equal(("Tên Mới", "+84987654321", "ja"), (updated.GetProperty("fullName").GetString(), updated.GetProperty("phoneNumber").GetString(), updated.GetProperty("preferredLang").GetString()));

        await using var check = _fixture.CreateContext();
        Assert.False(await check.Users.Where(u => u.Id == id).Select(u => u.IsPhoneVerified).SingleAsync());
    }

    [Fact]
    public async Task Ngay_sinh_doi_mot_lan_va_khoa_quanh_sinh_nhat()
    {
        var (_, _, access, _) = await CustomerAsync();
        var far = Today.AddDays(120).AddYears(-30);
        var other = Today.AddDays(150).AddYears(-30);
        var near = Today.AddDays(10).AddYears(-30);

        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Put, "/profile", access, Profile(dob: Today.AddDays(1)))).StatusCode);

        // Đặt lần đầu không tính là đổi
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Put, "/profile", access, Profile(dob: far))).StatusCode);
        Assert.Equal(ErrorCodes.DateOfBirthLocked, await AuthApiClient.ReadErrorCodeAsync(await SendAsync(HttpMethod.Put, "/profile", access, Profile(dob: near))));
        Assert.Equal(ErrorCodes.DateOfBirthLocked, await AuthApiClient.ReadErrorCodeAsync(await SendAsync(HttpMethod.Put, "/profile", access, Profile(dob: null))));
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Put, "/profile", access, Profile(dob: other))).StatusCode);
        Assert.Equal(ErrorCodes.DateOfBirthLocked, await AuthApiClient.ReadErrorCodeAsync(await SendAsync(HttpMethod.Put, "/profile", access, Profile(dob: far))));

        // Đang gần ngày sinh hiện tại thì không đổi được, kể cả chưa dùng lượt đổi
        var (_, _, second, _) = await CustomerAsync();
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Put, "/profile", second, Profile(dob: near))).StatusCode);
        Assert.Equal(ErrorCodes.DateOfBirthLocked, await AuthApiClient.ReadErrorCodeAsync(await SendAsync(HttpMethod.Put, "/profile", second, Profile(dob: far))));
    }

    [Fact]
    public async Task Doi_mat_khau_giu_thiet_bi_hien_tai_va_dang_xuat_thiet_bi_khac()
    {
        var (_, email, access, otherDevice) = await CustomerAsync();
        var next = AuthApiClient.DefaultPassword + "-moi";

        var wrong = await SendAsync(HttpMethod.Put, "/change-password", access, new { currentPassword = "sai", newPassword = next });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var changed = await SendAsync(HttpMethod.Put, "/change-password", access, new { currentPassword = AuthApiClient.DefaultPassword, newPassword = next });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _auth.RefreshAsync(otherDevice)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _auth.RefreshAsync(AuthApiClient.ReadRefreshCookie(changed))).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await _auth.LoginAsync(email)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _auth.LoginAsync(email, next)).StatusCode);
    }

    [Fact]
    public async Task Doi_email_chi_hieu_luc_sau_khi_xac_minh_va_khong_lo_email_da_co()
    {
        var (id, email, access, refresh) = await CustomerAsync();
        var (_, taken, _, _) = await CustomerAsync();
        var fresh = AuthApiClient.NewEmail();

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Post, "/change-email", access, new { currentPassword = "sai", newEmail = fresh })).StatusCode);

        Assert.Equal(HttpStatusCode.Accepted, (await SendAsync(HttpMethod.Post, "/change-email", access, new { currentPassword = AuthApiClient.DefaultPassword, newEmail = taken })).StatusCode);
        await using (var db = _fixture.CreateContext())
        {
            Assert.Null(await db.Users.Where(u => u.Id == id).Select(u => u.PendingEmail).SingleAsync());
        }

        Assert.Equal(HttpStatusCode.Accepted, (await SendAsync(HttpMethod.Post, "/change-email", access, new { currentPassword = AuthApiClient.DefaultPassword, newEmail = fresh.ToUpperInvariant() })).StatusCode);
        var profile = await AdminAuthApiClient.ReadDataAsync(await SendAsync(HttpMethod.Get, "/profile", access));
        Assert.Equal((email, fresh), (profile.GetProperty("email").GetString(), profile.GetProperty("pendingEmail").GetString()));
        await using (var db = _fixture.CreateContext())
        {
            Assert.True(await db.OutboxMessages.AnyAsync(m => m.IdempotencyKey.StartsWith("email_change_notice:") && m.PayloadJson.Contains(email)));
        }

        var token = await EmailTokenAsync("email_change_verify:", fresh);
        Assert.Equal(ErrorCodes.TokenInvalid, await AuthApiClient.ReadErrorCodeAsync(await _http.PostAsJsonAsync($"{Account}/confirm-email", new { token = "sai" })));
        Assert.Equal(HttpStatusCode.NoContent, (await _http.PostAsJsonAsync($"{Account}/confirm-email", new { token })).StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await AuthApiClient.ReadErrorCodeAsync(await _http.PostAsJsonAsync($"{Account}/confirm-email", new { token })));

        Assert.Equal(HttpStatusCode.Unauthorized, (await _auth.RefreshAsync(refresh)).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await _auth.LoginAsync(email)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _auth.LoginAsync(fresh)).StatusCode);
    }

    [Fact]
    public async Task Thiet_bi_dang_nhap_liet_ke_va_thu_hoi_duoc()
    {
        var (_, email, access, first) = await CustomerAsync();
        var other = await AuthApiClient.ReadAccessTokenAsync(await _auth.LoginAsync(email));
        var (_, _, stranger, _) = await CustomerAsync();

        var sessions = await AdminAuthApiClient.ReadDataAsync(await SendAsync(HttpMethod.Get, "/sessions", access));
        Assert.Equal(2, sessions.GetArrayLength());

        var oldest = sessions[1].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Delete, $"/sessions/{oldest}", stranger)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _auth.RefreshAsync(first)).StatusCode);
        var rotated = await AdminAuthApiClient.ReadDataAsync(await SendAsync(HttpMethod.Get, "/sessions", access));
        var target = rotated.EnumerateArray().First().GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, $"/sessions/{target}", access)).StatusCode);

        // Phiên vừa thu hồi là phiên của chính `access` ⇒ token đó hết dùng ngay (SEC-50); thiết bị còn lại vẫn dùng được
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/sessions", access)).StatusCode);
        Assert.Equal(1, (await AdminAuthApiClient.ReadDataAsync(await SendAsync(HttpMethod.Get, "/sessions", other))).GetArrayLength());

        var (_, _, all, one) = await CustomerAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, "/sessions/revoke-all", all)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _auth.RefreshAsync(one)).StatusCode);
    }

    [Fact]
    public async Task Bat_ban_tin_can_email_da_xac_thuc_va_co_bang_chung_dong_y()
    {
        var (id, _, access, _) = await CustomerAsync();

        Assert.Equal(ErrorCodes.EmailNotVerified, await AuthApiClient.ReadErrorCodeAsync(await SendAsync(HttpMethod.Put, "/newsletter", access, new { subscribed = true })));
        await using (var db = _fixture.CreateContext())
        {
            await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsEmailVerified, true));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Put, "/newsletter", access, new { subscribed = true })).StatusCode);
        Assert.True((await AdminAuthApiClient.ReadDataAsync(await SendAsync(HttpMethod.Get, "/newsletter", access))).GetProperty("subscribed").GetBoolean());
        await using (var db = _fixture.CreateContext())
        {
            Assert.Equal(1, await db.ConsentRecords.CountAsync(c => c.UserId == id && c.DocType == ConsentRecord.Marketing));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Put, "/newsletter", access, new { subscribed = false })).StatusCode);
        Assert.False((await AdminAuthApiClient.ReadDataAsync(await SendAsync(HttpMethod.Get, "/newsletter", access))).GetProperty("subscribed").GetBoolean());
    }

    private async Task RunDeletionAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AccountDeletionService>().AnonymizeDueAsync(CancellationToken.None);
    }

    private async Task ExpireGraceAsync(Guid id)
    {
        await using var db = _fixture.CreateContext();
        var past = DateTime.UtcNow.AddMinutes(-1);
        await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.DeletionScheduledAtUtc, past));
    }

    [Fact]
    public async Task Xoa_tai_khoan_co_7_ngay_cho_huy_duoc_roi_an_danh_hoa_giu_don_va_diem()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var (id, email, access, refresh) = await CustomerAsync();
        var orders = new OrderApiClient(_http);
        var orderEmail = AuthApiClient.NewEmail();
        Assert.Equal(HttpStatusCode.Created, (await orders.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Rice, 1, menu.Small)], email: orderEmail), access)).StatusCode);
        await SeedLinkedDataAsync(id, email);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Post, "/delete", access, new { password = "sai" })).StatusCode);
        var scheduled = (await AdminAuthApiClient.ReadDataAsync(await SendAsync(HttpMethod.Post, "/delete", access, new { password = AuthApiClient.DefaultPassword }))).GetDateTime();
        Assert.InRange((scheduled - DateTime.UtcNow).TotalDays, 6.9, 7.1);

        // Chưa tới hạn: job bỏ qua; hủy được
        await RunDeletionAsync();
        Assert.Equal(HttpStatusCode.OK, (await _auth.LoginAsync(email)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, "/delete/cancel", access)).StatusCode);
        await ExpireGraceAsync(id);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, "/delete/cancel", access)).StatusCode);
        await RunDeletionAsync();
        Assert.Equal(HttpStatusCode.OK, (await _auth.LoginAsync(email)).StatusCode);

        await SendAsync(HttpMethod.Post, "/delete", access, new { password = AuthApiClient.DefaultPassword });
        await ExpireGraceAsync(id);
        await RunDeletionAsync();

        Assert.NotEqual(HttpStatusCode.OK, (await _auth.LoginAsync(email)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _auth.RefreshAsync(refresh)).StatusCode);
        await AssertAnonymizedAsync(id, email, orderEmail);
    }

    private async Task SeedLinkedDataAsync(Guid id, string email)
    {
        var bookingEmail = AuthApiClient.NewEmail();
        var reservations = new ReservationApiClient(_http);
        Assert.Equal(HttpStatusCode.Created, (await reservations.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow, email: bookingEmail))).StatusCode);
        await using (var setup = _fixture.CreateContext())
        {
            await setup.Reservations.Where(r => r.GuestEmail == bookingEmail).ExecuteUpdateAsync(x => x.SetProperty(r => r.UserId, id));
        }

        await _http.PostAsJsonAsync("/api/v1/contact", new { fullName = "An", email, phoneNumber = "0901234567", subject = (string?)null, message = "Xin chào", lang = "vi", website = (string?)null });
        await _http.PostAsJsonAsync("/api/v1/newsletter/subscribe", new { email, lang = "vi", website = (string?)null });
    }

    private async Task AssertAnonymizedAsync(Guid id, string email, string orderEmail)
    {
        await using var db = _fixture.CreateContext();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
        Assert.True(user.IsDeleted);
        Assert.Equal("Khách đã xóa tài khoản", user.FullName);
        Assert.DoesNotContain(email, user.Email, StringComparison.Ordinal);
        Assert.Null(user.PhoneNumber);
        Assert.Null(user.PasswordHash);

        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.UserId == id);
        Assert.True(order.TotalAmount > 0);
        Assert.Equal("Khách đã xóa tài khoản", order.CustomerName);
        Assert.DoesNotContain(orderEmail, order.CustomerEmail, StringComparison.Ordinal);
        Assert.Empty(order.CustomerPhone);

        var booking = await db.Reservations.AsNoTracking().SingleAsync(r => r.UserId == id);
        Assert.Equal(("Khách đã xóa tài khoản", string.Empty), (booking.GuestName, booking.GuestPhone));
        Assert.False(await db.ContactMessages.AnyAsync(m => m.Email == email));
        Assert.False(await db.NewsletterSubscribers.AnyAsync(n => n.Email == email));
    }

    [Fact]
    public async Task Nhan_vien_dat_lich_xoa_can_step_up_va_ly_do()
    {
        var (id, _, _, _) = await CustomerAsync();
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var path = $"/api/v1/admin/customers/{id}/deletion";

        Assert.Equal(HttpStatusCode.Unauthorized, (await _admin.SendAsync(HttpMethod.Post, path, manager, new { reason = "Khách gọi điện" })).StatusCode);

        var stepUp = (await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, "/api/v1/admin/auth/step-up", manager, new { password = AdminAuthApiClient.Password, code = (string?)null }))).GetProperty("token").GetString();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { reason = "Khách gọi điện" }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", manager);
        request.Headers.Add("X-Step-Up-Token", stepUp);
        Assert.Equal(HttpStatusCode.NoContent, (await _http.SendAsync(request)).StatusCode);

        await using var db = _fixture.CreateContext();
        Assert.NotNull(await db.Users.Where(u => u.Id == id).Select(u => u.DeletionScheduledAtUtc).SingleAsync());
    }
}
