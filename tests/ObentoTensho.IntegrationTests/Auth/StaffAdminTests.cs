using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Auth;

// docs/srs/18 §2–§5, SEC-61: quản lý nhân viên, step-up, nhật ký audit
[Collection(SqlServerFixture.CollectionName)]
public sealed class StaffAdminTests : IDisposable
{
    private const string Staff = "/api/v1/admin/staff";
    private const string StepUp = "/api/v1/admin/auth/step-up";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public StaffAdminTests(SqlServerFixture fixture)
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

    private static string NewEmail() => $"moi.{Guid.NewGuid():N}@obento.test";

    private async Task<string> StepUpTokenAsync(string token)
    {
        var response = await _admin.SendAsync(HttpMethod.Post, StepUp, token, new { password = AdminAuthApiClient.Password, code = (string?)null });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await AdminAuthApiClient.ReadDataAsync(response)).GetProperty("token").GetString()!;
    }

    private Task<HttpResponseMessage> RiskyAsync(HttpMethod method, string path, string token, string? stepUp, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (stepUp is not null)
        {
            request.Headers.Add("X-Step-Up-Token", stepUp);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return _http.SendAsync(request);
    }

    private async Task<Guid> MyIdAsync(string token) =>
        (await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Staff}/me", token))).GetProperty("id").GetGuid();

    private async Task<string> InviteTokenAsync(string email)
    {
        await using var db = _fixture.CreateContext();
        var payload = await db.OutboxMessages
            .Where(m => m.IdempotencyKey.StartsWith("staff_invite:") && m.PayloadJson.Contains(email))
            .OrderByDescending(m => m.Id)
            .Select(m => m.PayloadJson)
            .FirstAsync();
        var link = JsonDocument.Parse(payload).RootElement.GetProperty("values").GetProperty("link").GetString()!;
        Assert.DoesNotContain("password\"", payload.Replace("\"link\"", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
        return Uri.UnescapeDataString(link[(link.IndexOf("#token=", StringComparison.Ordinal) + 7)..]);
    }

    private Task<HttpResponseMessage> AcceptAsync(string token) =>
        _http.PostAsJsonAsync("/api/v1/admin/auth/accept-invite", new { token, password = AdminAuthApiClient.Password + "x" });

    // Nhân viên không bắt buộc 2FA, đăng nhập lấy access token và refresh token
    private async Task<(Guid Id, string Email, string Access, string Refresh)> LoggedInCashierAsync()
    {
        var (id, email) = await _admin.CreateStaffAsync(StaffRole.Cashier);
        var login = await _admin.LoginAsync(email);
        var refresh = AdminAuthApiClient.ReadAdminCookie(login)!;
        var access = (await AdminAuthApiClient.ReadDataAsync(login)).GetProperty("session").GetProperty("accessToken").GetString()!;
        return (id, email, access, refresh);
    }

    [Fact]
    public async Task Thao_tac_rui_ro_can_step_up_cua_chinh_minh()
    {
        var superAdmin = await _admin.SignInAsync(StaffRole.SuperAdmin);
        var other = await _admin.SignInAsync(StaffRole.SuperAdmin);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var body = new { fullName = "Bếp Mới", email = NewEmail(), role = "kitchen" };

        var missing = await RiskyAsync(HttpMethod.Post, Staff, superAdmin, null, body);
        Assert.Equal(ErrorCodes.StepUpRequired, await AuthApiClient.ReadErrorCodeAsync(missing));
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        var wrong = await _admin.SendAsync(HttpMethod.Post, StepUp, superAdmin, new { password = "sai-mat-khau", code = (string?)null });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _admin.SendAsync(HttpMethod.Post, StepUp, superAdmin, new { password = (string?)null, code = (string?)null })).StatusCode);

        // Mã của người khác, hoặc mã tạm đăng nhập, không dùng được
        var foreign = await StepUpTokenAsync(other);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RiskyAsync(HttpMethod.Post, Staff, superAdmin, foreign, body)).StatusCode);
        var myEmail = (await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Staff}/me", superAdmin))).GetProperty("email").GetString()!;
        var challenge = (await AdminAuthApiClient.ReadDataAsync(await _admin.LoginAsync(myEmail))).GetProperty("challenge").GetProperty("challengeToken").GetString();
        Assert.Equal(HttpStatusCode.Unauthorized, (await RiskyAsync(HttpMethod.Post, Staff, superAdmin, challenge, body)).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await RiskyAsync(HttpMethod.Post, Staff, superAdmin, await StepUpTokenAsync(superAdmin), body)).StatusCode);

        // Manager không có quyền dù đã step-up
        Assert.Equal(HttpStatusCode.Forbidden, (await RiskyAsync(HttpMethod.Post, Staff, manager, await StepUpTokenAsync(manager), body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, Staff, manager)).StatusCode);
    }

    [Fact]
    public async Task Moi_nhan_vien_bang_lien_ket_mot_lan_72_gio()
    {
        var superAdmin = await _admin.SignInAsync(StaffRole.SuperAdmin);
        var stepUp = await StepUpTokenAsync(superAdmin);
        var email = NewEmail();

        var created = await RiskyAsync(HttpMethod.Post, Staff, superAdmin, stepUp, new { fullName = "Thu Ngân Mới", email = email.ToUpperInvariant(), role = "cashier" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = await AdminAuthApiClient.ReadDataAsync(created);
        Assert.True(dto.GetProperty("invitePending").GetBoolean());
        var id = dto.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await RiskyAsync(HttpMethod.Post, Staff, superAdmin, stepUp, new { fullName = "Trùng", email, role = "cashier" })).StatusCode);

        var first = await InviteTokenAsync(email);

        // Hết hạn thì bị từ chối, gửi lại thì liên kết cũ mất hiệu lực
        await using (var db = _fixture.CreateContext())
        {
            var past = DateTime.UtcNow.AddMinutes(-1);
            await db.StaffMembers.Where(s => s.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.InviteExpiryUtc, past));
        }

        Assert.Equal(ErrorCodes.TokenInvalid, await AuthApiClient.ReadErrorCodeAsync(await AcceptAsync(first)));
        Assert.Equal(HttpStatusCode.NoContent, (await RiskyAsync(HttpMethod.Post, $"{Staff}/{id}/resend-invite", superAdmin, stepUp)).StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await AuthApiClient.ReadErrorCodeAsync(await AcceptAsync(first)));

        var second = await InviteTokenAsync(email);
        Assert.Equal(HttpStatusCode.NoContent, (await AcceptAsync(second)).StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await AuthApiClient.ReadErrorCodeAsync(await AcceptAsync(second)));
        Assert.Equal(HttpStatusCode.OK, (await _admin.LoginAsync(email, AdminAuthApiClient.Password + "x")).StatusCode);
        Assert.Equal(ErrorCodes.InviteAlreadyAccepted, await AuthApiClient.ReadErrorCodeAsync(await RiskyAsync(HttpMethod.Post, $"{Staff}/{id}/resend-invite", superAdmin, stepUp)));

        // Vô hiệu hóa rồi cũng không dùng lại được email (BR-STF-16)
        Assert.Equal(HttpStatusCode.OK, (await RiskyAsync(HttpMethod.Patch, $"{Staff}/{id}/status", superAdmin, stepUp, new { isActive = false })).StatusCode);
        Assert.Equal(ErrorCodes.EmailTaken, await AuthApiClient.ReadErrorCodeAsync(await RiskyAsync(HttpMethod.Post, Staff, superAdmin, stepUp, new { fullName = "Trùng", email, role = "cashier" })));
    }

    [Fact]
    public async Task Khong_tu_khoa_va_khong_ha_SuperAdmin_cuoi_cung()
    {
        var superAdmin = await _admin.SignInAsync(StaffRole.SuperAdmin);
        var id = await MyIdAsync(superAdmin);
        var stepUp = await StepUpTokenAsync(superAdmin);

        var self = await RiskyAsync(HttpMethod.Patch, $"{Staff}/{id}/status", superAdmin, stepUp, new { isActive = false });
        Assert.Equal(ErrorCodes.CannotChangeSelf, await AuthApiClient.ReadErrorCodeAsync(self));

        await using (var db = _fixture.CreateContext())
        {
            await db.StaffMembers.Where(s => s.Role == StaffRole.SuperAdmin && s.Id != id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        }

        var demoted = await RiskyAsync(HttpMethod.Put, $"{Staff}/{id}", superAdmin, stepUp, new { fullName = "Quản Lý", role = "manager" });
        Assert.Equal(ErrorCodes.LastSuperAdmin, await AuthApiClient.ReadErrorCodeAsync(demoted));
        Assert.Equal(HttpStatusCode.Conflict, demoted.StatusCode);

        // Đổi tên thì vẫn được
        Assert.Equal(HttpStatusCode.OK, (await RiskyAsync(HttpMethod.Put, $"{Staff}/{id}", superAdmin, stepUp, new { fullName = "Chủ Quán", role = "super_admin" })).StatusCode);
    }

    [Fact]
    public async Task Khoa_doi_vai_tro_thu_hoi_phien_co_hieu_luc_ngay()
    {
        var superAdmin = await _admin.SignInAsync(StaffRole.SuperAdmin);
        var stepUp = await StepUpTokenAsync(superAdmin);

        var locked = await LoggedInCashierAsync();
        Assert.Equal(HttpStatusCode.OK, (await RiskyAsync(HttpMethod.Patch, $"{Staff}/{locked.Id}/status", superAdmin, stepUp, new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _admin.RefreshAsync(locked.Refresh)).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await _admin.LoginAsync(locked.Email)).StatusCode);

        var moved = await LoggedInCashierAsync();
        Assert.Equal(HttpStatusCode.OK, (await RiskyAsync(HttpMethod.Put, $"{Staff}/{moved.Id}", superAdmin, stepUp, new { fullName = "Lễ Tân", role = "reception" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _admin.RefreshAsync(moved.Refresh)).StatusCode);

        var renamed = await LoggedInCashierAsync();
        Assert.Equal(HttpStatusCode.OK, (await RiskyAsync(HttpMethod.Put, $"{Staff}/{renamed.Id}", superAdmin, stepUp, new { fullName = "Tên Mới", role = "cashier" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _admin.RefreshAsync(renamed.Refresh)).StatusCode);

        var revoked = await LoggedInCashierAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await RiskyAsync(HttpMethod.Post, $"{Staff}/{revoked.Id}/revoke-sessions", superAdmin, stepUp)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _admin.RefreshAsync(revoked.Refresh)).StatusCode);
    }

    [Fact]
    public async Task Dat_lai_2FA_bat_thiet_lap_lai_khi_dang_nhap()
    {
        var superAdmin = await _admin.SignInAsync(StaffRole.SuperAdmin);
        var stepUp = await StepUpTokenAsync(superAdmin);
        var (id, email) = await _admin.CreateStaffAsync(StaffRole.Manager);
        await using (var db = _fixture.CreateContext())
        {
            await db.StaffMembers.Where(s => s.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.TotpEnabled, true));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await RiskyAsync(HttpMethod.Post, $"{Staff}/{id}/reset-2fa", superAdmin, stepUp)).StatusCode);

        var challenge = (await AdminAuthApiClient.ReadDataAsync(await _admin.LoginAsync(email))).GetProperty("challenge");
        Assert.Equal("totp_setup", challenge.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Doi_mat_khau_cua_minh_thu_hoi_phien_va_lam_step_up_cu_mat_hieu_luc()
    {
        var cashier = await LoggedInCashierAsync();
        var superAdmin = await _admin.SignInAsync(StaffRole.SuperAdmin);
        var stepUp = await StepUpTokenAsync(superAdmin);
        var path = "/api/v1/admin/auth/me/change-password";
        var next = AdminAuthApiClient.Password + "-moi";

        var me = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Staff}/me", cashier.Access));
        Assert.Contains(me.GetProperty("capabilities").EnumerateArray(), c => string.Equals(c.GetString(), "orders_cashier", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.Unauthorized, (await _admin.SendAsync(HttpMethod.Post, path, cashier.Access, new { currentPassword = "sai", newPassword = next })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Post, path, cashier.Access, new { currentPassword = AdminAuthApiClient.Password, newPassword = next })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _admin.RefreshAsync(cashier.Refresh)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _admin.LoginAsync(cashier.Email, next)).StatusCode);

        var saId = await MyIdAsync(superAdmin);
        Assert.Equal(HttpStatusCode.NoContent, (await RiskyAsync(HttpMethod.Post, $"{Staff}/{saId}/reset-2fa", superAdmin, stepUp)).StatusCode);

        // Security stamp đã đổi nên mã step-up cũ vô hiệu; access token có thể còn sống vài giây trong bộ đệm
        var stale = await RiskyAsync(HttpMethod.Post, $"{Staff}/{saId}/revoke-sessions", superAdmin, stepUp);
        Assert.Contains(stale.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized });
    }

    [Fact]
    public async Task Nhat_ky_chi_SuperAdmin_xem_loc_duoc_va_khong_lo_bi_mat()
    {
        var superAdmin = await _admin.SignInAsync(StaffRole.SuperAdmin);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var stepUp = await StepUpTokenAsync(superAdmin);
        var email = NewEmail();
        var created = await AdminAuthApiClient.ReadDataAsync(await RiskyAsync(HttpMethod.Post, Staff, superAdmin, stepUp, new { fullName = "Bị Kiểm Toán", email, role = "kitchen" }));
        var id = created.GetProperty("id").GetGuid();
        await RiskyAsync(HttpMethod.Put, $"{Staff}/{id}", superAdmin, stepUp, new { fullName = "Tên Khác", role = "kitchen" });

        var (cashierId, cashierEmail) = await _admin.CreateStaffAsync(StaffRole.Cashier);
        for (var i = 0; i < 3; i++)
        {
            await _admin.LoginAsync(cashierEmail, "mat-khau-sai-thu");
        }

        const string Logs = "/api/v1/admin/audit-logs";
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, Logs, manager)).StatusCode);

        var rows = await ListAsync($"{Logs}?entityType=Staff&entityId={id}", superAdmin);
        var update = rows.Single(r => string.Equals(r.GetProperty("action").GetString(), "staff.Staff.updated", StringComparison.Ordinal));
        Assert.Contains("Bị Kiểm Toán", update.GetProperty("oldDataJson").GetString()!, StringComparison.Ordinal);
        Assert.Contains("Tên Khác", update.GetProperty("newDataJson").GetString()!, StringComparison.Ordinal);
        Assert.Equal(await MyIdAsync(superAdmin), update.GetProperty("actorId").GetGuid());
        Assert.DoesNotContain(email, update.ToString(), StringComparison.OrdinalIgnoreCase);

        var failed = await ListAsync($"{Logs}?action=auth.staff.login_failed&entityId={cashierId}", superAdmin);
        Assert.Equal(3, failed.Length);

        var today = TestSchedule.TodayVn.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var yesterday = TestSchedule.TodayVn.AddDays(-1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        Assert.NotEmpty(await ListAsync($"{Logs}?entityId={id}&from={today}&to={today}", superAdmin));
        Assert.Empty(await ListAsync($"{Logs}?entityId={id}&to={yesterday}", superAdmin));
    }

    private async Task<JsonElement[]> ListAsync(string path, string token)
    {
        var body = await (await _admin.SendAsync(HttpMethod.Get, path, token)).Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").EnumerateArray().ToArray();
    }
}
