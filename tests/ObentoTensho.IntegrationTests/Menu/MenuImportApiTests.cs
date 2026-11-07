using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Menu;

// docs/srs/15 §8 (BR-MNU-10, BR-MNU-14): xuất/nhập Excel qua API thật — xem trước, tất cả hoặc không, báo lỗi đúng dòng
[Collection(SqlServerFixture.CollectionName)]
public sealed class MenuImportApiTests : IDisposable
{
    private const string Items = "/api/v1/admin/menu/items/";

    private static readonly string[] FishOnly = ["fish"];

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly AdminAuthApiClient _api;

    public MenuImportApiTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _api = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Xem_truoc_khong_ghi_roi_ghi_that_tao_mon_moi()
    {
        var manager = await _api.SignInAsync(StaffRole.Manager);
        var category = await CreateCategoryAsync(manager);
        var tag = Unique();
        var file = Workbook(
            Row(category, $"Cơm gà {tag}", "65.000", allergens: "egg"),
            Row(category, $"Cơm gà {tag}", "70000"),
            Row(category, $"Trà đá {tag}", "5000", status: "Ẩn"));

        var preview = await ImportAsync(manager, file, commit: false);
        Assert.False(preview.GetProperty("committed").GetBoolean());
        Assert.Equal(3, preview.GetProperty("created").GetInt32());
        Assert.Equal(0, await CountAsync(tag));

        var committed = await ImportAsync(manager, file, commit: true);
        Assert.True(committed.GetProperty("committed").GetBoolean());
        var slugs = committed.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("slug").GetString()).ToArray();
        Assert.Equal(3, slugs.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, await CountAsync(tag));

        // Món ẩn không hiện ở trang công khai; món đang bán hiện ngay (cache đã xóa)
        using var anonymous = _factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/v1/menu/items/{slugs[0]}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/menu/items/{slugs[2]}")).StatusCode);
    }

    [Fact]
    public async Task Mot_dong_loi_thi_khong_ghi_dong_nao_va_bao_dung_dong_cot()
    {
        var manager = await _api.SignInAsync(StaffRole.Manager);
        var category = await CreateCategoryAsync(manager);
        var tag = Unique();
        var file = Workbook(
            Row(category, $"Bún bò {tag}", "55000"),
            Row(category, $"Bún chả {tag}", "-5000"),
            Row("khong-co-danh-muc", $"Phở {tag}", "60000", original: "50000"));

        var report = await ImportAsync(manager, file, commit: true);

        Assert.False(report.GetProperty("committed").GetBoolean());
        Assert.Equal(2, report.GetProperty("failed").GetInt32());
        var rows = report.GetProperty("rows");
        Assert.Empty(rows[0].GetProperty("errors").EnumerateArray());
        Assert.Equal(3, rows[1].GetProperty("row").GetInt32());
        Assert.Equal([(MenuSheet.Price, FieldErrorCodes.InvalidFormat)], Errors(rows[1]));
        Assert.Equal(
            [(MenuSheet.Category, FieldErrorCodes.NotFound), (MenuSheet.OriginalPrice, FieldErrorCodes.OutOfRange)],
            Errors(rows[2]).Order());
        Assert.Equal(0, await CountAsync(tag));
    }

    [Fact]
    public async Task Xuat_sua_roi_nhap_lai_cap_nhat_mon_va_giu_phan_khong_co_trong_tep()
    {
        var manager = await _api.SignInAsync(StaffRole.Manager);
        var category = await CreateCategoryAsync(manager);
        var name = $"Cơm cá {Unique()}";
        var created = await _api.SendAsync(HttpMethod.Post, Items, manager, new
        {
            categoryId = category.Id, nameVi = name, fullDescHtml = "<p>Cá hồi</p>", price = 90_000m,
            labels = new { isNew = false, isBestseller = false, isFeatured = false, isVegetarian = false, isSpicy = false },
            allowTakeaway = true, status = "available", allergens = FishOnly, optionGroupIds = Array.Empty<int>(),
        });
        var item = await AdminAuthApiClient.ReadDataAsync(created);
        var slug = item.GetProperty("slug").GetString()!;

        var export = await _api.SendAsync(HttpMethod.Get, Items + "export", manager);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.StartsWith("thuc-don-", export.Content.Headers.ContentDisposition?.FileNameStar, StringComparison.Ordinal);
        var bytes = await export.Content.ReadAsByteArrayAsync();

        // Sửa giá đúng món này trong tệp vừa xuất, nhập lại
        using (var book = new XLWorkbook(new MemoryStream(bytes)))
        {
            var sheet = book.Worksheet(MenuSheet.ItemsSheet);
            var row = sheet.RowsUsed().Single(r => string.Equals(r.Cell(1).GetString(), slug, StringComparison.Ordinal));
            row.Cell(5).Value = 95_000;
            using var output = new MemoryStream();
            book.SaveAs(output);
            bytes = output.ToArray();
        }

        var report = await ImportAsync(manager, bytes, commit: true);
        Assert.True(report.GetProperty("committed").GetBoolean(), report.ToString());
        Assert.Equal(0, report.GetProperty("created").GetInt32());

        var after = await AdminAuthApiClient.ReadDataAsync(await _api.SendAsync(HttpMethod.Get, $"{Items}{item.GetProperty("id").GetInt32()}", manager));
        Assert.Equal(95_000m, after.GetProperty("price").GetDecimal());
        Assert.Equal("<p>Cá hồi</p>", after.GetProperty("fullDescHtml").GetString());
        Assert.Equal("fish", after.GetProperty("allergens")[0].GetString());

        await using var db = _fixture.CreateContext();
        var audit = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == "menu.menu_item.updated" && a.EntityId == item.GetProperty("id").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture))
            .SingleAsync();
        Assert.Contains("90000", audit.OldDataJson, StringComparison.Ordinal);
        Assert.Contains("95000", audit.NewDataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tep_sai_hoac_qua_lon_bi_tu_choi_va_chi_quan_ly_duoc_dung()
    {
        var manager = await _api.SignInAsync(StaffRole.Manager);
        var reception = await _api.SignInAsync(StaffRole.Reception);

        Assert.Equal(ErrorCodes.FileInvalid, await ErrorOf(_api.UploadAsync(Items + "import", manager, "menu.xlsx", "khong phai excel"u8.ToArray())));
        var tooBig = await _api.UploadAsync(Items + "import", manager, "menu.xlsx", new byte[(int)MenuSheet.MaxBytes + 1]);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooBig.StatusCode);

        var template = await _api.SendAsync(HttpMethod.Get, Items + "import-template", manager);
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        var empty = await ImportAsync(manager, await template.Content.ReadAsByteArrayAsync(), commit: true);
        Assert.Equal(0, empty.GetProperty("total").GetInt32());
        Assert.False(empty.GetProperty("committed").GetBoolean());

        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Get, Items + "export", reception)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.UploadAsync(Items + "import", reception, "menu.xlsx", Workbook())).StatusCode);
    }

    [Fact]
    public async Task Than_request_vuot_gioi_han_tren_Kestrel_tra_413_khong_phai_500()
    {
        using var factory = new ApiFactory(_fixture.ConnectionString);
        factory.UseRealServer();
        var api = new AdminAuthApiClient(factory, _fixture);
        var manager = await api.SignInAsync(StaffRole.Manager);

        var response = await api.UploadAsync(Items + "import", manager, "menu.xlsx", new byte[(int)MenuSheet.MaxBytes + (128 * 1024)]);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(ErrorCodes.FileTooLarge, await AuthApiClient.ReadErrorCodeAsync(response));

        // Không có Content-Length: Kestrel phát hiện khi đang đọc và ném lỗi — phải ra 413, không phải 500
        var chunked = await api.UploadAsync(Items + "import", manager, "menu.xlsx", new byte[(int)MenuSheet.MaxBytes + (128 * 1024)], chunked: true);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, chunked.StatusCode);
        Assert.Equal(ErrorCodes.FileTooLarge, await AuthApiClient.ReadErrorCodeAsync(chunked));
    }

    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    private static (string Column, string? Code)[] Errors(JsonElement row) =>
        row.GetProperty("errors").EnumerateArray().Select(e => (e.GetProperty("column").GetString()!, e.GetProperty("code").GetString())).ToArray();

    private static Dictionary<string, string?> Row((int Id, string Slug) category, string name, string price, string? original = null, string? status = null, string? allergens = null) =>
        Row(category.Slug, name, price, original, status, allergens);

    private static Dictionary<string, string?> Row(string categorySlug, string name, string price, string? original = null, string? status = null, string? allergens = null) =>
        new(StringComparer.Ordinal)
        {
            [MenuSheet.Category] = categorySlug,
            [MenuSheet.Name] = name,
            [MenuSheet.Price] = price,
            [MenuSheet.OriginalPrice] = original,
            [MenuSheet.Status] = status,
            [MenuSheet.Allergens] = allergens,
        };

    private static byte[] Workbook(params Dictionary<string, string?>[] rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet(MenuSheet.ItemsSheet);
        for (var c = 0; c < MenuSheet.Columns.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = MenuSheet.Columns[c];
            for (var r = 0; r < rows.Length; r++)
            {
                sheet.Cell(r + 2, c + 1).Value = rows[r].GetValueOrDefault(MenuSheet.Columns[c]);
            }
        }

        using var output = new MemoryStream();
        book.SaveAs(output);
        return output.ToArray();
    }

    private async Task<JsonElement> ImportAsync(string token, byte[] file, bool commit)
    {
        var response = await _api.UploadAsync($"{Items}import?commit={(commit ? "true" : "false")}", token, "thuc-don.xlsx", file);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await AdminAuthApiClient.ReadDataAsync(response);
    }

    private async Task<(int Id, string Slug)> CreateCategoryAsync(string token)
    {
        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/menu/categories", token, new { name = $"Nhập {Unique()}", isActive = true });
        var data = await AdminAuthApiClient.ReadDataAsync(response);
        return (data.GetProperty("id").GetInt32(), data.GetProperty("slug").GetString()!);
    }

    private async Task<int> CountAsync(string tag)
    {
        await using var db = _fixture.CreateContext();
        return await db.MenuItems.CountAsync(i => i.NameVi.Contains(tag));
    }

    private static async Task<string?> ErrorOf(Task<HttpResponseMessage> request) =>
        await AuthApiClient.ReadErrorCodeAsync(await request);
}
