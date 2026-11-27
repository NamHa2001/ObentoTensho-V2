using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Orders;

// Mua mang về tại quầy (chủ quán 2026-11-13): không gắn bàn; khách thường trả tiền trước rồi chờ món —
// đã trả tiền thì vẫn ở màn hình bếp, món cuối ra xong mới hoàn thành; không chuyển/gộp bàn, không gọi thêm sau khi trả
[Collection(SqlServerFixture.CollectionName)]
public sealed class CounterOrderTests : IDisposable
{
    private const string Api = "/api/v1/admin/dine-in";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public CounterOrderTests(SqlServerFixture fixture)
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

    private static async Task<JsonElement> OkAsync(Task<HttpResponseMessage> response)
    {
        var message = await response;
        Assert.True(message.IsSuccessStatusCode, await message.Content.ReadAsStringAsync());
        return await AdminAuthApiClient.ReadDataAsync(message);
    }

    private Task<HttpResponseMessage> Post(string path, string token, object? body = null) =>
        _admin.SendAsync(HttpMethod.Post, $"{Api}{path}", token, body ?? new { });

    private async Task<JsonElement?> TicketAsync(string token, Guid id) =>
        (await OkAsync(_admin.SendAsync(HttpMethod.Get, $"{Api}/kitchen-view", token))).EnumerateArray()
            .Cast<JsonElement?>()
            .FirstOrDefault(t => t!.Value.GetProperty("billId").GetGuid() == id);

    [Fact]
    public async Task Tra_tien_la_hoan_tat_ngay_mon_chua_ra_van_o_man_hinh_bep()
    {
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var reception = await _admin.SignInAsync(StaffRole.Reception);
        var kitchen = await _admin.SignInAsync(StaffRole.Kitchen);

        var bill = await OkAsync(Post("/counter", reception, new { customerName = "Chị Lan", note = (string?)null }));
        var id = bill.GetProperty("id").GetGuid();
        Assert.Equal("counter", bill.GetProperty("channel").GetString());
        Assert.Equal("Mang về · Chị Lan", bill.GetProperty("tableName").GetString());

        bill = await OkAsync(Post($"/{id}/items", reception, new { items = new[] { OrderApiClient.Line(menu.Tea, 2), OrderApiClient.Line(menu.DineInOnly, 1) } }));
        var receipt = await OkAsync(Post($"/{id}/checkout", reception, new { paymentMethod = "cash" }));
        Assert.Equal(100_000, receipt.GetProperty("totalAmount").GetDecimal());

        // Chủ quán 2026-11-27: đã trả tiền là Hoàn tất ngay, rời tab Mang về; món chưa ra vẫn ở bếp; không gọi thêm hay thanh toán lại
        await using (var db = _fixture.CreateContext())
        {
            Assert.Equal(OrderStatus.Completed, await db.Orders.Where(o => o.Id == id).Select(o => o.Status).SingleAsync());
        }

        Assert.DoesNotContain((await OkAsync(_admin.SendAsync(HttpMethod.Get, $"{Api}/counter", reception))).EnumerateArray(), c => c.GetProperty("id").GetGuid() == id);
        Assert.NotNull(await TicketAsync(kitchen, id));
        Assert.Equal(ErrorCodes.NotFound, await AuthApiClient.ReadErrorCodeAsync(await Post($"/{id}/items", reception, new { items = new[] { OrderApiClient.Line(menu.Tea, 1) } })));
        Assert.Equal(ErrorCodes.NotFound, await AuthApiClient.ReadErrorCodeAsync(await Post($"/{id}/checkout", reception, new { paymentMethod = "cash" })));

        await OkAsync(Post($"/{id}/items/{bill.GetProperty("items")[0].GetProperty("id").GetInt64()}/served", kitchen));
        Assert.NotNull(await TicketAsync(kitchen, id));

        await OkAsync(Post($"/{id}/items/{bill.GetProperty("items")[1].GetProperty("id").GetInt64()}/served", kitchen));
        Assert.Null(await TicketAsync(kitchen, id));
        await using var check = _fixture.CreateContext();
        var order = await check.Orders.SingleAsync(o => o.Id == id);
        Assert.Equal((OrderStatus.Completed, PaymentStatus.Paid, OrderChannel.Counter, (int?)null), (order.Status, order.PaymentStatus, order.Channel, order.TableId));
    }

    // Dữ liệu trước 2026-11-27: đơn quầy đã trả tiền mà còn kẹt ở "Đang chuẩn bị" được việc nền chuyển Hoàn tất, ngày hoàn thành = lúc trả tiền
    [Fact]
    public async Task Viec_nen_chuyen_don_quay_da_tra_tien_con_mo_sang_hoan_tat_theo_gio_tra()
    {
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var reception = await _admin.SignInAsync(StaffRole.Reception);
        var id = (await OkAsync(Post("/counter", reception, new { customerName = "Anh Bình", note = (string?)null }))).GetProperty("id").GetGuid();
        await OkAsync(Post($"/{id}/items", reception, new { items = new[] { OrderApiClient.Line(menu.Tea, 1) } }));
        await OkAsync(Post($"/{id}/checkout", reception, new { paymentMethod = "cash" }));

        var paidAt = new DateTime(2026, 11, 20, 5, 0, 0, DateTimeKind.Utc);
        await using (var db = _fixture.CreateContext())
        {
            await db.Orders.Where(o => o.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Preparing)
                .SetProperty(o => o.CompletedAtUtc, (DateTime?)null)
                .SetProperty(o => o.PaidAtUtc, paidAt));
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<ObentoTensho.Core.Orders.Services.DineInService>().CompletePaidBillsAsync(CancellationToken.None) >= 1);
        }

        await using var check = _fixture.CreateContext();
        var order = await check.Orders.SingleAsync(o => o.Id == id);
        Assert.Equal((OrderStatus.Completed, (DateTime?)paidAt), (order.Status, order.CompletedAtUtc));
    }

    [Fact]
    public async Task Ra_mon_xong_roi_moi_tra_tien_thi_hoan_thanh_ngay_va_khong_chuyen_ban_duoc()
    {
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var id = (await OkAsync(Post("/counter", cashier, new { customerName = (string?)null, note = (string?)null }))).GetProperty("id").GetGuid();
        var bill = await OkAsync(Post($"/{id}/items", cashier, new { items = new[] { OrderApiClient.Line(menu.Tea, 1) } }));
        Assert.Equal("Mang về · Khách mang về", bill.GetProperty("tableName").GetString());

        // Đơn tại quầy không chuyển sang bàn được
        int tableId;
        await using (var setup = _fixture.CreateContext())
        {
            var area = new ObentoTensho.Core.Tables.Domain.TableArea { Name = $"Quầy {Guid.NewGuid():N}"[..20] };
            setup.TableAreas.Add(area);
            await setup.SaveChangesAsync();
            var table = new ObentoTensho.Core.Tables.Domain.DiningTable { AreaId = area.Id, Name = "Q1", MinCapacity = 1, MaxCapacity = 2 };
            setup.Tables.Add(table);
            await setup.SaveChangesAsync();
            tableId = table.Id;
        }

        Assert.Equal(ErrorCodes.InvalidTransition, await AuthApiClient.ReadErrorCodeAsync(await Post($"/{id}/move", cashier, new { tableId })));
        await OkAsync(Post($"/{id}/items/{bill.GetProperty("items")[0].GetProperty("id").GetInt64()}/served-at-table", cashier));
        await OkAsync(Post($"/{id}/checkout", cashier, new { paymentMethod = "bank_transfer" }));

        await using var db = _fixture.CreateContext();
        Assert.Equal(OrderStatus.Completed, await db.Orders.Where(o => o.Id == id).Select(o => o.Status).SingleAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.SendAsync(HttpMethod.Get, $"{Api}/{id}", cashier)).StatusCode);
    }
}
