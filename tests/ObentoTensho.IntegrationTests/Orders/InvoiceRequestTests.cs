using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Tables.Domain;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Orders;

// Yêu cầu xuất hóa đơn (chủ quán 2026-11-13): gửi kèm đơn online, lúc thanh toán tại bàn, hoặc thu ngân thêm sau;
// kế toán (quản lý) xem với CCCD che, hiện đầy đủ có audit, ghi số hóa đơn, xuất Excel
[Collection(SqlServerFixture.CollectionName)]
public sealed class InvoiceRequestTests : IDisposable
{
    private const string Admin = "/api/v1/admin";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;
    private readonly OrderApiClient _orders;

    public InvoiceRequestTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _admin = new AdminAuthApiClient(_factory, fixture);
        _orders = new OrderApiClient(_http);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    private static object Company(string taxCode = "0312345678") =>
        new { buyerType = "company", buyerName = "Công ty TNHH Ăn Ngon", nationalId = (string?)null, taxCode, address = "12 Lê Lợi, Quận 1, TP.HCM", email = "ketoan@anngon.vn", phone = (string?)null, note = (string?)null };

    private static object Person(string nationalId = "079200001234") =>
        new { buyerType = "individual", buyerName = "Nguyễn Văn An", nationalId, taxCode = (string?)null, address = (string?)null, email = (string?)null, phone = "0912345678", note = (string?)null };

    private static async Task<JsonElement> OkAsync(Task<HttpResponseMessage> response)
    {
        var message = await response;
        Assert.True(message.IsSuccessStatusCode, await message.Content.ReadAsStringAsync());
        return await AdminAuthApiClient.ReadDataAsync(message);
    }

    private async Task<JsonElement> FindAsync(string token, string orderCode)
    {
        var page = await (await _admin.SendAsync(HttpMethod.Get, $"{Admin}/invoice-requests?q={orderCode}", token)).Content.ReadFromJsonAsync<JsonElement>();
        return page.GetProperty("data").EnumerateArray().Single();
    }

    [Fact]
    public async Task Thanh_toan_tai_ban_kem_hoa_don_doanh_nghiep_ke_toan_thay_va_ghi_so_hoa_don()
    {
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        int tableId;
        await using (var db = _fixture.CreateContext())
        {
            var area = new TableArea { Name = $"Tầng {Guid.NewGuid():N}"[..20] };
            db.TableAreas.Add(area);
            await db.SaveChangesAsync();
            var table = new DiningTable { AreaId = area.Id, Name = "HĐ1", MinCapacity = 1, MaxCapacity = 4 };
            db.Tables.Add(table);
            await db.SaveChangesAsync();
            tableId = table.Id;
        }

        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var bill = await OkAsync(_admin.SendAsync(HttpMethod.Post, $"{Admin}/dine-in", cashier, new { tableId, guestCount = 2 }));
        var id = bill.GetProperty("id").GetGuid();
        await OkAsync(_admin.SendAsync(HttpMethod.Post, $"{Admin}/dine-in/{id}/items", cashier, new { items = new[] { OrderApiClient.Line(menu.Tea, 2) } }));

        // MST sai thì không thanh toán, lỗi chỉ đúng ô
        var bad = await _admin.SendAsync(HttpMethod.Post, $"{Admin}/dine-in/{id}/checkout", cashier, new { paymentMethod = "cash", invoice = Company("12345") });
        Assert.Equal(ErrorCodes.ValidationFailed, await AuthApiClient.ReadErrorCodeAsync(bad));
        Assert.Contains("invoice.taxCode", await bad.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var receipt = await OkAsync(_admin.SendAsync(HttpMethod.Post, $"{Admin}/dine-in/{id}/checkout", cashier, new { paymentMethod = "cash", invoice = Company("0312345678-001") }));
        var code = receipt.GetProperty("code").GetString()!;

        // Thu ngân không xem danh sách hóa đơn; kế toán (quản lý) xem được
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, $"{Admin}/invoice-requests", cashier)).StatusCode);
        var row = await FindAsync(manager, code);
        Assert.Equal("0312345678001", row.GetProperty("taxCode").GetString());
        Assert.Equal("dine_in", row.GetProperty("channel").GetString());
        Assert.Equal(10_000, row.GetProperty("totalAmount").GetDecimal());

        var requestId = row.GetProperty("id").GetGuid();
        Assert.Equal(ErrorCodes.ValidationFailed, await AuthApiClient.ReadErrorCodeAsync(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/invoice-requests/{requestId}/issue", manager, new { invoiceNumber = " " })));
        var issued = await OkAsync(_admin.SendAsync(HttpMethod.Post, $"{Admin}/invoice-requests/{requestId}/issue", manager, new { invoiceNumber = "C26TAA 0000123" }));
        Assert.Equal("issued", issued.GetProperty("status").GetString());

        // Đã xuất thì không hủy ở đây (phải điều chỉnh trên phần mềm hóa đơn điện tử)
        Assert.Equal(ErrorCodes.InvalidTransition, await AuthApiClient.ReadErrorCodeAsync(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/invoice-requests/{requestId}/cancel", manager, new { reason = "Nhầm" })));
    }

    [Fact]
    public async Task Don_online_kem_hoa_don_ca_nhan_CCCD_che_hien_day_du_co_audit_va_xuat_Excel()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var order = JsonSerializer.SerializeToElement(OrderApiClient.Order([OrderApiClient.Line(menu.Tea, 1)]));
        var body = order.EnumerateObject().ToDictionary(p => p.Name, p => (object)p.Value, StringComparer.Ordinal);

        body["invoice"] = Person("12345");
        var bad = await _orders.CreateAsync(body);
        Assert.Contains("invoice.nationalId", await bad.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        body["invoice"] = Person();
        var created = await OkAsync(_orders.CreateAsync(body));
        var code = created.GetProperty("order").GetProperty("code").GetString()!;

        var row = await FindAsync(manager, code);
        Assert.Equal("*********234", row.GetProperty("maskedNationalId").GetString());
        var requestId = row.GetProperty("id").GetGuid();
        var contact = await OkAsync(_admin.SendAsync(HttpMethod.Post, $"{Admin}/invoice-requests/{requestId}/reveal", manager));
        Assert.Equal("079200001234", contact.GetProperty("nationalId").GetString());
        Assert.Equal("+84912345678", contact.GetProperty("phone").GetString());

        var file = await _admin.SendAsync(HttpMethod.Get, $"{Admin}/invoice-requests/export?q={code}", manager);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.Content.Headers.ContentType?.MediaType);

        await using var db = _fixture.CreateContext();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "invoices.request.contact_revealed" && a.EntityId == requestId.ToString()));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "invoices.request.exported"));
    }

    [Fact]
    public async Task Thu_ngan_them_yeu_cau_cho_don_co_san_moi_don_mot_yeu_cau_huy_roi_them_lai_duoc()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var created = await OkAsync(_orders.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Tea, 1)])));
        var orderId = created.GetProperty("order").GetProperty("id").GetGuid();
        var path = $"{Admin}/orders/{orderId}/invoice-request";

        var first = await OkAsync(_admin.SendAsync(HttpMethod.Post, path, cashier, Company()));
        Assert.Equal(ErrorCodes.InvoiceExists, await AuthApiClient.ReadErrorCodeAsync(await _admin.SendAsync(HttpMethod.Post, path, cashier, Person())));

        var requestId = first.GetProperty("id").GetGuid();
        Assert.Equal(ErrorCodes.ValidationFailed, await AuthApiClient.ReadErrorCodeAsync(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/invoice-requests/{requestId}/cancel", manager, new { reason = "" })));
        await OkAsync(_admin.SendAsync(HttpMethod.Post, $"{Admin}/invoice-requests/{requestId}/cancel", manager, new { reason = "Khách nhập sai MST" }));
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Post, path, cashier, Company("0101234567"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/orders/{Guid.NewGuid()}/invoice-request", cashier, Company())).StatusCode);
    }
}
