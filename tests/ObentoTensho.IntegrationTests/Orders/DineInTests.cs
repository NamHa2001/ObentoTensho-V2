using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Tables.Domain;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Orders;

// Gọi món tại bàn (chủ quán 2026-11-13): một bàn một phiếu, gọi nhiều đợt gửi bếp, hủy món cần quản lý,
// chuyển/gộp bàn, thanh toán dùng chung giảm giá và tích điểm với đơn mang về
[Collection(SqlServerFixture.CollectionName)]
public sealed class DineInTests : IDisposable
{
    private const string Api = "/api/v1/admin/dine-in";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public DineInTests(SqlServerFixture fixture)
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

    private async Task<int[]> TablesAsync(int count)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        await using var db = _fixture.CreateContext();
        var area = new TableArea { Name = $"Tầng {tag}" };
        db.TableAreas.Add(area);
        await db.SaveChangesAsync();
        var tables = Enumerable.Range(1, count)
            .Select(i => new DiningTable { AreaId = area.Id, Name = $"{tag}-{i}", MinCapacity = 1, MaxCapacity = 4 })
            .ToArray();
        db.Tables.AddRange(tables);
        await db.SaveChangesAsync();
        return tables.Select(t => t.Id).ToArray();
    }

    private async Task<TableStatus> TableStatusAsync(int tableId)
    {
        await using var db = _fixture.CreateContext();
        return await db.Tables.Where(t => t.Id == tableId).Select(t => t.CurrentStatus).SingleAsync();
    }

    private static async Task<JsonElement> OkAsync(Task<HttpResponseMessage> response)
    {
        var message = await response;
        Assert.True(message.IsSuccessStatusCode, await message.Content.ReadAsStringAsync());
        return await AdminAuthApiClient.ReadDataAsync(message);
    }

    private static async Task<string?> ErrorOf(Task<HttpResponseMessage> response) => await AuthApiClient.ReadErrorCodeAsync(await response);

    private Task<HttpResponseMessage> Post(string path, string token, object? body = null) =>
        _admin.SendAsync(HttpMethod.Post, $"{Api}{path}", token, body ?? new { });

    private static object Line(int menuItemId, int quantity, params int[] optionIds) => new { menuItemId, quantity, optionIds, note = (string?)null };

    private static long ItemId(JsonElement bill, int index) => bill.GetProperty("items")[index].GetProperty("id").GetInt64();

    [Fact]
    public async Task Mo_ban_goi_hai_dot_bep_ra_mon_quan_ly_huy_mon_roi_thu_ngan_thanh_toan()
    {
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var tables = await TablesAsync(1);
        var reception = await _admin.SignInAsync(StaffRole.Reception);
        var kitchen = await _admin.SignInAsync(StaffRole.Kitchen);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var manager = await _admin.SignInAsync(StaffRole.Manager);

        var bill = await OkAsync(Post(string.Empty, reception, new { tableId = tables[0], guestCount = 3, note = "Gần cửa sổ" }));
        var id = bill.GetProperty("id").GetGuid();
        Assert.Equal(TableStatus.Occupied, await TableStatusAsync(tables[0]));

        // Bàn đang có phiếu thì không mở thêm; bếp không mở bàn được
        Assert.Equal(ErrorCodes.TableConflict, await ErrorOf(Post(string.Empty, cashier, new { tableId = tables[0], guestCount = 2 })));
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(string.Empty, kitchen, new { tableId = tables[0], guestCount = 2 })).StatusCode);

        // Đợt 1: cơm cỡ lớn ×2 và món chỉ bán tại quán; đợt 2: trà
        bill = await OkAsync(Post($"/{id}/items", reception, new { items = new[] { Line(menu.Rice, 2, menu.Large), Line(menu.DineInOnly, 1) } }));
        Assert.Equal(2 * 80_000 + 90_000, bill.GetProperty("subTotal").GetDecimal());
        Assert.Equal(ErrorCodes.ItemUnavailable, await ErrorOf(Post($"/{id}/items", reception, new { items = new[] { Line(menu.SoldOut, 1) } })));
        bill = await OkAsync(Post($"/{id}/items", reception, new { items = new[] { Line(menu.Tea, 1) } }));
        Assert.Equal(2, bill.GetProperty("items")[2].GetProperty("round").GetInt32());

        // Bếp thấy phiếu theo bàn, không có tiền; ra món thì món đó rời màn hình bếp
        var ticket = (await OkAsync(_admin.SendAsync(HttpMethod.Get, $"{Api}/kitchen-view", kitchen))).EnumerateArray().Single(t => t.GetProperty("billId").GetGuid() == id);
        Assert.Equal(3, ticket.GetProperty("items").GetArrayLength());
        Assert.False(ticket.GetProperty("items")[0].TryGetProperty("lineTotal", out _));
        await OkAsync(Post($"/{id}/items/{ItemId(bill, 0)}/served", kitchen));
        ticket = (await OkAsync(_admin.SendAsync(HttpMethod.Get, $"{Api}/kitchen-view", kitchen))).EnumerateArray().Single(t => t.GetProperty("billId").GetGuid() == id);
        Assert.Equal(2, ticket.GetProperty("items").GetArrayLength());

        // Hủy món đã gửi bếp: thu ngân không được, quản lý được (phải có lý do), món hủy không tính tiền
        var tea = ItemId(bill, 2);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post($"/{id}/items/{tea}/void", cashier, new { reason = "Khách đổi ý" })).StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, await ErrorOf(Post($"/{id}/items/{tea}/void", manager, new { reason = " " })));
        bill = await OkAsync(Post($"/{id}/items/{tea}/void", manager, new { reason = "Khách đổi ý" }));
        Assert.Equal(250_000, bill.GetProperty("subTotal").GetDecimal());
        Assert.True(bill.GetProperty("items")[2].GetProperty("voided").GetBoolean());

        // Còn món thì không hủy phiếu được; lễ tân không thu tiền được
        Assert.Equal(ErrorCodes.BillNotEmpty, await ErrorOf(Post($"/{id}/cancel", reception, new { reason = "Mở nhầm" })));
        Assert.Equal(HttpStatusCode.Forbidden, (await Post($"/{id}/checkout", reception, new { paymentMethod = "cash" })).StatusCode);

        var receipt = await OkAsync(Post($"/{id}/checkout", cashier, new { paymentMethod = "bank_transfer" }));
        Assert.Equal(250_000, receipt.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(TableStatus.Cleaning, await TableStatusAsync(tables[0]));
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.SendAsync(HttpMethod.Get, $"{Api}/{id}", cashier)).StatusCode);

        await using var db = _fixture.CreateContext();
        var order = await db.Orders.SingleAsync(o => o.Id == id);
        Assert.Equal((OrderStatus.Completed, PaymentStatus.Paid, OrderChannel.DineIn), (order.Status, order.PaymentStatus, order.Channel));

    }

    [Fact]
    public async Task Ban_dang_don_bao_don_xong_thi_tro_lai_trong()
    {
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var tables = await TablesAsync(1);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var id = (await OkAsync(Post(string.Empty, cashier, new { tableId = tables[0], guestCount = 2 }))).GetProperty("id").GetGuid();

        // Bàn còn phiếu mở thì không báo dọn xong được
        Assert.Equal(ErrorCodes.InvalidTransition, await ErrorOf(Post($"/tables/{tables[0]}/clean", cashier)));
        await OkAsync(Post($"/{id}/items", cashier, new { items = new[] { Line(menu.Tea, 1) } }));
        await OkAsync(Post($"/{id}/checkout", cashier, new { paymentMethod = "cash" }));
        Assert.Equal(TableStatus.Cleaning, await TableStatusAsync(tables[0]));

        Assert.Equal(HttpStatusCode.NoContent, (await Post($"/tables/{tables[0]}/clean", cashier)).StatusCode);
        Assert.Equal(TableStatus.Available, await TableStatusAsync(tables[0]));
        Assert.Equal(HttpStatusCode.OK, (await Post(string.Empty, cashier, new { tableId = tables[0], guestCount = 2 })).StatusCode);
    }

    [Fact]
    public async Task Thanh_vien_theo_SDT_duoc_giam_theo_hang_va_tich_diem()
    {
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var tables = await TablesAsync(1);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var auth = new AuthApiClient(_http);
        var email = AuthApiClient.NewEmail();
        var phone = AuthApiClient.NewPhone();
        (await auth.RegisterAsync(email, phone: phone)).EnsureSuccessStatusCode();
        await using (var db = _fixture.CreateContext())
        {
            await db.Users.Where(u => u.Email == email).ExecuteUpdateAsync(u => u.SetProperty(x => x.MemberTier, MemberTier.Gold));
        }

        var id = (await OkAsync(Post(string.Empty, cashier, new { tableId = tables[0], guestCount = 2 }))).GetProperty("id").GetGuid();
        await OkAsync(Post($"/{id}/items", cashier, new { items = new[] { Line(menu.DineInOnly, 2) } }));

        Assert.Equal(ErrorCodes.MemberNotFound, await ErrorOf(Post($"/{id}/quote", cashier, new { memberPhone = AuthApiClient.NewPhone() })));
        var quote = await OkAsync(Post($"/{id}/quote", cashier, new { memberPhone = phone }));
        Assert.Equal(180_000 * 5 / 100, quote.GetProperty("memberDiscount").GetDecimal());

        var receipt = await OkAsync(Post($"/{id}/checkout", cashier, new { paymentMethod = "cash", memberPhone = phone }));
        Assert.Equal(171_000, receipt.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(17, receipt.GetProperty("pointsEarned").GetInt32());

        await using var check = _fixture.CreateContext();
        var userId = await check.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        Assert.Equal(userId, await check.Orders.Where(o => o.Id == id).Select(o => o.UserId).SingleAsync());
    }

    [Fact]
    public async Task Chuyen_ban_va_gop_ban()
    {
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var tables = await TablesAsync(3);
        var reception = await _admin.SignInAsync(StaffRole.Reception);

        var first = (await OkAsync(Post(string.Empty, reception, new { tableId = tables[0], guestCount = 2 }))).GetProperty("id").GetGuid();
        var second = (await OkAsync(Post(string.Empty, reception, new { tableId = tables[1], guestCount = 3 }))).GetProperty("id").GetGuid();
        await OkAsync(Post($"/{first}/items", reception, new { items = new[] { Line(menu.Tea, 1) } }));
        await OkAsync(Post($"/{second}/items", reception, new { items = new[] { Line(menu.Tea, 2) } }));

        // Chuyển sang bàn đang có phiếu bị chặn; sang bàn trống thì bàn cũ chuyển dọn dẹp
        Assert.Equal(ErrorCodes.TableConflict, await ErrorOf(Post($"/{first}/move", reception, new { tableId = tables[1] })));
        var moved = await OkAsync(Post($"/{first}/move", reception, new { tableId = tables[2] }));
        Assert.Equal(tables[2], moved.GetProperty("tableId").GetInt32());
        Assert.Equal(TableStatus.Cleaning, await TableStatusAsync(tables[0]));

        // Gộp phiếu bàn 2 vào phiếu đã chuyển: cộng món và số khách, phiếu nguồn hủy
        var merged = await OkAsync(Post($"/{first}/merge", reception, new { fromBillId = second }));
        Assert.Equal(5, merged.GetProperty("guestCount").GetInt32());
        Assert.Equal(15_000, merged.GetProperty("subTotal").GetDecimal());
        Assert.Equal(TableStatus.Cleaning, await TableStatusAsync(tables[1]));

        await using var db = _fixture.CreateContext();
        Assert.Equal(OrderStatus.Cancelled, await db.Orders.Where(o => o.Id == second).Select(o => o.Status).SingleAsync());
    }

    [Fact]
    public async Task Hai_nguoi_mo_cung_mot_ban_cung_luc_chi_mot_phieu()
    {
        var tables = await TablesAsync(1);
        var reception = await _admin.SignInAsync(StaffRole.Reception);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Post(string.Empty, reception, new { tableId = tables[0], guestCount = 2 })));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        await using var db = _fixture.CreateContext();
        Assert.Equal(1, await db.Orders.CountAsync(o => o.TableId == tables[0] && o.Status == OrderStatus.Preparing));
    }

    [Fact]
    public async Task Phieu_chua_goi_mon_huy_duoc_va_ban_tro_lai_trong()
    {
        var tables = await TablesAsync(1);
        var reception = await _admin.SignInAsync(StaffRole.Reception);
        var id = (await OkAsync(Post(string.Empty, reception, new { tableId = tables[0], guestCount = 2 }))).GetProperty("id").GetGuid();

        Assert.Equal(ErrorCodes.ValidationFailed, await ErrorOf(Post($"/{id}/cancel", reception, new { reason = "" })));
        Assert.Equal(HttpStatusCode.NoContent, (await Post($"/{id}/cancel", reception, new { reason = "Khách về" })).StatusCode);
        Assert.Equal(TableStatus.Available, await TableStatusAsync(tables[0]));
    }
}
