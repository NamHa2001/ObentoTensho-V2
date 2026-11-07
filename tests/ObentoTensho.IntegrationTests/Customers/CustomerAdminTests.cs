using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Customers.Services;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Customers;

// docs/srs/18 §1, BR-CUS-01..09: danh sách, chi tiết, khóa, xuất có kiểm soát
[Collection(SqlServerFixture.CollectionName)]
public sealed class CustomerAdminTests : IDisposable
{
    private const string Customers = "/api/v1/admin/customers";
    private const string Exports = "/api/v1/admin/exports";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;
    private readonly AuthApiClient _auth;

    public CustomerAdminTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _admin = new AdminAuthApiClient(_factory, fixture);
        _auth = new AuthApiClient(_http);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    private async Task<(Guid Id, string Email, string Refresh)> CustomerAsync()
    {
        var email = AuthApiClient.NewEmail();
        var (_, refresh) = await _auth.RegisterAndLoginAsync(email);
        await using var db = _fixture.CreateContext();
        var id = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        return (id, email, refresh);
    }

    private async Task<string> StepUpAsync(string token)
    {
        var response = await _admin.SendAsync(HttpMethod.Post, "/api/v1/admin/auth/step-up", token, new { password = AdminAuthApiClient.Password, code = (string?)null });
        return (await AdminAuthApiClient.ReadDataAsync(response)).GetProperty("token").GetString()!;
    }

    private Task<HttpResponseMessage> ExportAsync(string token, string? stepUp, string path = $"{Customers}/export", string reason = "Đối soát cuối tháng")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { reason }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (stepUp is not null)
        {
            request.Headers.Add("X-Step-Up-Token", stepUp);
        }

        return _http.SendAsync(request);
    }

    private async Task RunExportJobAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DataExportJob>().RunAsync(CancellationToken.None);
    }

    private async Task<string> DownloadTokenAsync(Guid exportId)
    {
        await using var db = _fixture.CreateContext();
        var key = $"data_export_ready:{exportId:N}";
        var payload = await db.OutboxMessages.Where(m => m.IdempotencyKey == key).Select(m => m.PayloadJson).SingleAsync();
        var link = JsonDocument.Parse(payload).RootElement.GetProperty("values").GetProperty("link").GetString()!;
        return Uri.UnescapeDataString(link[(link.IndexOf("#token=", StringComparison.Ordinal) + 7)..]);
    }

    [Fact]
    public async Task Danh_sach_che_lien_he_chi_tiet_day_du_va_co_audit()
    {
        var (id, email, _) = await CustomerAsync();
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);

        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, Customers, cashier)).StatusCode);

        var list = await (await _admin.SendAsync(HttpMethod.Get, $"{Customers}?q={email}&pageSize=1000", manager)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(100, list.GetProperty("meta").GetProperty("pageSize").GetInt32());
        var row = Assert.Single(list.GetProperty("data").EnumerateArray());
        Assert.DoesNotContain(email, row.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("***", row.GetProperty("emailMasked").GetString()!, StringComparison.Ordinal);
        Assert.StartsWith("+84*", row.GetProperty("phoneMasked").GetString()!, StringComparison.Ordinal);

        var tiers = await (await _admin.SendAsync(HttpMethod.Get, $"{Customers}?q={email}&tier=gold", manager)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, tiers.GetProperty("data").GetArrayLength());

        var detail = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Customers}/{id}", manager));
        Assert.Equal(email, detail.GetProperty("email").GetString());
        Assert.Equal(0, detail.GetProperty("recentOrders").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.SendAsync(HttpMethod.Get, $"{Customers}/{Guid.NewGuid()}", manager)).StatusCode);

        await using var db = _fixture.CreateContext();
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "customers.User.viewed" && a.EntityId == id.ToString());
        Assert.NotNull(audit.ActorId);
    }

    [Fact]
    public async Task Khoa_can_ly_do_thu_hoi_phien_gui_thu_va_mo_lai_duoc()
    {
        var (id, email, refresh) = await CustomerAsync();
        var (_, _, untouched) = await CustomerAsync();
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var path = $"{Customers}/{id}/status";

        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Patch, path, manager, new { isActive = false, reason = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Patch, path, manager, new { isActive = false, reason = "Lạm dụng voucher" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _auth.RefreshAsync(untouched)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _auth.RefreshAsync(refresh)).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await _auth.LoginAsync(email)).StatusCode);

        await using (var db = _fixture.CreateContext())
        {
            var audit = await db.AuditLogs.SingleAsync(a => a.Action == "customers.User.status_changed" && a.EntityId == id.ToString());
            Assert.Contains("Lạm dụng voucher", audit.NewDataJson!, StringComparison.Ordinal);
            Assert.True(await db.OutboxMessages.AnyAsync(m => m.IdempotencyKey.StartsWith("account_locked:") && m.PayloadJson.Contains(email)));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Patch, path, manager, new { isActive = true, reason = "Đã xác minh" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _auth.LoginAsync(email)).StatusCode);
    }

    [Fact]
    public async Task Xuat_khach_can_step_up_ly_do_chay_nen_mot_lan_mot_ngay_va_chi_nguoi_yeu_cau_tai_duoc()
    {
        var (_, email, _) = await CustomerAsync();
        await _admin.SignInAsync(StaffRole.SuperAdmin);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var other = await _admin.SignInAsync(StaffRole.Manager);
        var stepUp = await StepUpAsync(manager);

        Assert.Equal(ErrorCodes.StepUpRequired, await AuthApiClient.ReadErrorCodeAsync(await ExportAsync(manager, null)));
        Assert.Equal(HttpStatusCode.BadRequest, (await ExportAsync(manager, stepUp, reason: " ")).StatusCode);

        var created = await ExportAsync(manager, stepUp);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await AdminAuthApiClient.ReadDataAsync(created)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.TooManyRequests, (await ExportAsync(manager, stepUp)).StatusCode);

        await RunExportJobAsync();

        var mine = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, Exports, manager));
        Assert.Equal("ready", mine[0].GetProperty("status").GetString());

        var token = await DownloadTokenAsync(id);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, $"{Exports}/download", other, new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, $"{Exports}/download", manager, new { token = "sai" })).StatusCode);

        var file = await _admin.SendAsync(HttpMethod.Post, $"{Exports}/download", manager, new { token });
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        using (var book = new XLWorkbook(await file.Content.ReadAsStreamAsync()))
        {
            Assert.Contains(book.Worksheet(1).RowsUsed(), r => string.Equals(r.Cell(2).GetString(), email, StringComparison.Ordinal));
        }

        await using (var db = _fixture.CreateContext())
        {
            var key = id.ToString();
            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "customers.DataExport.requested" && a.EntityId == key));
            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "customers.DataExport.downloaded" && a.EntityId == key));
            Assert.True(await db.OutboxMessages.AnyAsync(m => m.IdempotencyKey.StartsWith($"admin_alert:data-export:{id:N}:")));

            // Quá 24 giờ: tệp bị xóa, dòng còn lại
            var past = DateTime.UtcNow.AddMinutes(-1);
            await db.DataExports.Where(e => e.Id == id).ExecuteUpdateAsync(s => s.SetProperty(e => e.ExpiresAtUtc, past));
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, $"{Exports}/download", manager, new { token })).StatusCode);
        await RunExportJobAsync();
        await using (var db = _fixture.CreateContext())
        {
            var row = await db.DataExports.AsNoTracking().SingleAsync(e => e.Id == id);
            Assert.Equal((DataExportStatus.Expired, (byte[]?)null), (row.Status, row.Content));
        }

        // Newsletter đi qua cùng luồng, tính hạn mức riêng
        var news = await ExportAsync(manager, stepUp, "/api/v1/admin/newsletter/subscribers/export");
        Assert.Equal(HttpStatusCode.Created, news.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ExportAsync(await _admin.SignInAsync(StaffRole.ContentEditor), stepUp, "/api/v1/admin/newsletter/subscribers/export")).StatusCode);
    }
}
