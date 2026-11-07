using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Menu;

// docs/srs/15 §8 và docs/srs/22: thực đơn qua API thật — quản trị, trang công khai, cache, bản dịch
[Collection(SqlServerFixture.CollectionName)]
public sealed class MenuApiTests : IDisposable
{
    private const string Admin = "/api/v1/admin/menu/";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly AdminAuthApiClient _api;
    private readonly HttpClient _public;

    public MenuApiTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _api = new AdminAuthApiClient(_factory, fixture);
        _public = _factory.CreateApiClient();
    }

    public void Dispose()
    {
        _public.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Tao_mon_co_tuy_chon_hien_ngay_tren_trang_cong_khai()
    {
        var manager = await _api.SignInAsync(StaffRole.Manager);
        var (categoryId, categorySlug) = await CreateCategoryAsync(manager);
        var groupId = await CreateSizeGroupAsync(manager);

        // Giá gốc ≤ giá bán ⇒ 400; HTML mô tả được làm sạch (SEC-08)
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.SendAsync(HttpMethod.Post, Admin + "items", manager, ItemBody(categoryId, "Cơm gà", [groupId]) with { OriginalPrice = 65_000m })).StatusCode);
        var created = await _api.SendAsync(HttpMethod.Post, Admin + "items", manager, ItemBody(categoryId, $"Cơm gà Hội An {Unique()}", [groupId]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = await AdminAuthApiClient.ReadDataAsync(created);
        var slug = item.GetProperty("slug").GetString()!;
        Assert.StartsWith("com-ga-hoi-an-", slug, StringComparison.Ordinal);
        Assert.Equal("<p><strong>Gà</strong> ta</p>", item.GetProperty("fullDescHtml").GetString());

        // Cache công khai đã bị xóa khi tạo món nên món hiện ngay (BR-MNU-09)
        var list = await PublicDataAsync($"/api/v1/menu/items?category={categorySlug}");
        var card = Assert.Single(list.EnumerateArray());
        Assert.Equal(slug, card.GetProperty("slug").GetString());
        Assert.Equal(["egg", "soy"], card.GetProperty("allergens").EnumerateArray().Select(a => a.GetString()), StringComparer.Ordinal);

        var detail = await PublicDataAsync($"/api/v1/menu/items/{slug}");
        var group = Assert.Single(detail.GetProperty("optionGroups").EnumerateArray());
        Assert.Equal("one", group.GetProperty("selectionType").GetString());
        Assert.Equal(2, group.GetProperty("options").GetArrayLength());

        // Lọc dị ứng: bỏ món có trứng; mã lạ không làm hỏng trang
        Assert.Equal(0, (await PublicDataAsync($"/api/v1/menu/items?category={categorySlug}&excludeAllergens=egg")).GetArrayLength());
        Assert.Equal(1, (await PublicDataAsync($"/api/v1/menu/items?category={categorySlug}&excludeAllergens=abc,%3Cx%3E")).GetArrayLength());

        var categories = await PublicDataAsync("/api/v1/menu/categories");
        Assert.Equal(1, categories.EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == categoryId).GetProperty("itemCount").GetInt32());
    }

    [Fact]
    public async Task An_het_hang_va_xoa_mon_doi_trang_cong_khai_ngay()
    {
        var manager = await _api.SignInAsync(StaffRole.Manager);
        var (categoryId, categorySlug) = await CreateCategoryAsync(manager);
        var item = await AdminAuthApiClient.ReadDataAsync(await _api.SendAsync(HttpMethod.Post, Admin + "items", manager, ItemBody(categoryId, $"Mì udon {Unique()}", [])));
        var id = item.GetProperty("id").GetInt32();
        var slug = item.GetProperty("slug").GetString()!;
        Assert.Equal(1, (await PublicDataAsync($"/api/v1/menu/items?category={categorySlug}")).GetArrayLength());

        // Hết hàng tới mốc đã qua ⇒ vẫn hiển thị là đang bán (BR-MNU-16)
        await SetStatusAsync(manager, id, new { status = "sold_out", soldOutUntilUtc = DateTime.UtcNow.AddMinutes(-1) });
        Assert.Equal("available", (await PublicDataAsync($"/api/v1/menu/items/{slug}")).GetProperty("item").GetProperty("status").GetString());
        await SetStatusAsync(manager, id, new { status = "sold_out", soldOutUntilUtc = DateTime.UtcNow.AddHours(2) });
        Assert.Equal("sold_out", (await PublicDataAsync($"/api/v1/menu/items/{slug}")).GetProperty("item").GetProperty("status").GetString());

        // Ẩn ⇒ biến mất khỏi danh sách và chi tiết (BR-MNU-02)
        await SetStatusAsync(manager, id, new { status = "hidden" });
        Assert.Equal(0, (await PublicDataAsync($"/api/v1/menu/items?category={categorySlug}")).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await _public.GetAsync($"/api/v1/menu/items/{slug}")).StatusCode);

        // Danh mục còn món (kể cả món đã xóa mềm) không xóa được (BR-MNU-08)
        var current = await _api.SendAsync(HttpMethod.Get, $"{Admin}items/{id}", manager);
        Assert.Equal(HttpStatusCode.NoContent, (await _api.SendAsync(HttpMethod.Delete, $"{Admin}items/{id}", manager, ifMatch: current.Headers.ETag?.Tag)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Get, $"{Admin}items/{id}", manager)).StatusCode);
        var categoryETag = await CategoryVersionAsync(manager, categoryId);
        Assert.Equal(ErrorCodes.CategoryHasItems, await ErrorOf(_api.SendAsync(HttpMethod.Delete, $"{Admin}categories/{categoryId}", manager, ifMatch: categoryETag)));

        // Slug của món đã xóa vẫn giữ chỗ: tạo lại cùng tên ra slug khác
        var again = await AdminAuthApiClient.ReadDataAsync(await _api.SendAsync(HttpMethod.Post, Admin + "items", manager, ItemBody(categoryId, item.GetProperty("nameVi").GetString()!, [])));
        Assert.NotEqual(slug, again.GetProperty("slug").GetString(), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Ban_dich_chi_hien_khi_da_duyet_va_con_khop_ban_goc()
    {
        var manager = await _api.SignInAsync(StaffRole.Manager);
        var (categoryId, _) = await CreateCategoryAsync(manager);
        var created = await _api.SendAsync(HttpMethod.Post, Admin + "items", manager, ItemBody(categoryId, $"Cơm bò {Unique()}", []));
        var item = await AdminAuthApiClient.ReadDataAsync(created);
        var id = item.GetProperty("id").GetInt32();
        var slug = item.GetProperty("slug").GetString()!;

        // Bản nháp ⇒ khách tiếng Anh vẫn thấy bản Việt kèm cờ dự phòng
        Assert.Equal(HttpStatusCode.OK, (await PutTranslationAsync(manager, id, approve: false)).StatusCode);
        var draft = (await PublicDataAsync($"/api/v1/menu/items/{slug}?lang=en")).GetProperty("item");
        Assert.True(draft.GetProperty("isFallback").GetBoolean());
        Assert.Equal("vi", draft.GetProperty("contentLang").GetString());

        // Duyệt ⇒ hiện tiếng Anh, và tìm được bằng tên tiếng Anh (DB-17)
        Assert.Equal(HttpStatusCode.OK, (await PutTranslationAsync(manager, id, approve: true)).StatusCode);
        var approved = (await PublicDataAsync($"/api/v1/menu/items/{slug}?lang=en")).GetProperty("item");
        Assert.Equal("Beef rice", approved.GetProperty("name").GetString());
        Assert.False(approved.GetProperty("isFallback").GetBoolean());
        var adminSearch = await _api.SendAsync(HttpMethod.Get, Admin + "items?q=beef%20rice", manager);
        Assert.Contains((await ReadPagedAsync(adminSearch)).EnumerateArray(), i => i.GetProperty("id").GetInt32() == id);

        // Sửa bản Việt ⇒ bản dịch lỗi thời, quay về bản Việt (BR-I18N-02)
        // Duyệt bản dịch cập nhật chuỗi tìm kiếm của món nên ETag lúc tạo đã cũ
        var body = ItemBody(categoryId, item.GetProperty("nameVi").GetString()!, []) with { ShortDesc = "Bò Úc nướng" };
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await _api.SendAsync(HttpMethod.Put, $"{Admin}items/{id}", manager, body, created.Headers.ETag?.Tag)).StatusCode);
        var current = await _api.SendAsync(HttpMethod.Get, $"{Admin}items/{id}", manager);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Put, $"{Admin}items/{id}", manager, body, current.Headers.ETag?.Tag)).StatusCode);
        Assert.True((await PublicDataAsync($"/api/v1/menu/items/{slug}?lang=en")).GetProperty("item").GetProperty("isFallback").GetBoolean());
        var translations = await AdminAuthApiClient.ReadDataAsync(await _api.SendAsync(HttpMethod.Get, $"{Admin}items/{id}/translations", manager));
        Assert.True(translations[0].GetProperty("outdated").GetBoolean());
    }

    [Fact]
    public async Task Nhom_tuy_chon_dang_dung_khong_xoa_duoc_va_chi_quan_ly_duoc_sua_thuc_don()
    {
        var manager = await _api.SignInAsync(StaffRole.Manager);
        var reception = await _api.SignInAsync(StaffRole.Reception);
        var (categoryId, _) = await CreateCategoryAsync(manager);
        var groupId = await CreateSizeGroupAsync(manager);
        await _api.SendAsync(HttpMethod.Post, Admin + "items", manager, ItemBody(categoryId, $"Cơm cá {Unique()}", [groupId]));

        Assert.Equal(ErrorCodes.OptionGroupInUse, await ErrorOf(_api.SendAsync(HttpMethod.Delete, $"{Admin}option-groups/{groupId}", manager)));
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Get, Admin + "items", reception)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Post, Admin + "categories", reception, new { name = "X", isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _public.GetAsync(Admin + "items")).StatusCode);

        await using var db = _fixture.CreateContext();
        Assert.True(await db.AuditLogs.AsNoTracking().AnyAsync(a => a.Action == "menu.menu_item.created"));
    }

    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    private static ItemRequest ItemBody(int categoryId, string name, int[] groupIds) => new(
        categoryId,
        name,
        65_000m,
        null,
        "<p onclick=\"x()\"><strong>Gà</strong> ta</p><script>alert(1)</script>",
        new { isNew = true, isBestseller = false, isFeatured = false, isVegetarian = false, isSpicy = false },
        "available",
        ["soy", "egg"],
        groupIds);

    private async Task<(int Id, string Slug)> CreateCategoryAsync(string token)
    {
        var response = await _api.SendAsync(HttpMethod.Post, Admin + "categories", token, new { name = $"Cơm hộp {Unique()}", isActive = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await AdminAuthApiClient.ReadDataAsync(response);
        return (data.GetProperty("id").GetInt32(), data.GetProperty("slug").GetString()!);
    }

    private async Task<int> CreateSizeGroupAsync(string token)
    {
        var group = await _api.SendAsync(HttpMethod.Post, Admin + "option-groups", token, new { name = "Size", selectionType = "one", isRequired = true, minSelect = 1, maxSelect = 1, isActive = true });
        Assert.Equal(HttpStatusCode.Created, group.StatusCode);
        var groupId = (await AdminAuthApiClient.ReadDataAsync(group)).GetProperty("id").GetInt32();
        foreach (var (name, delta) in new[] { ("Nhỏ", 0m), ("Lớn", 15_000m) })
        {
            var option = await _api.SendAsync(HttpMethod.Post, $"{Admin}option-groups/{groupId}/options", token, new { name, priceDelta = delta, isDefault = delta == 0, status = "available", allergens = Array.Empty<string>() });
            Assert.Equal(HttpStatusCode.Created, option.StatusCode);
        }

        return groupId;
    }

    private async Task SetStatusAsync(string token, int id, object body) =>
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Patch, $"{Admin}items/{id}/status", token, body)).StatusCode);

    private async Task<string?> CategoryVersionAsync(string token, int categoryId)
    {
        var list = await AdminAuthApiClient.ReadDataAsync(await _api.SendAsync(HttpMethod.Get, Admin + "categories", token));
        var version = list.EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == categoryId).GetProperty("version").GetString();
        return $"\"{version}\"";
    }

    private Task<HttpResponseMessage> PutTranslationAsync(string token, int id, bool approve) =>
        _api.SendAsync(HttpMethod.Put, $"{Admin}items/{id}/translations/en", token, new { name = "Beef rice", shortDesc = "Tender beef", approve });

    private async Task<JsonElement> PublicDataAsync(string path)
    {
        var response = await _public.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("data").Clone();
    }

    private static async Task<JsonElement> ReadPagedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("data").Clone();
    }

    private static async Task<string?> ErrorOf(Task<HttpResponseMessage> request) =>
        await AuthApiClient.ReadErrorCodeAsync(await request);

    // Thân yêu cầu tạo/sửa món (dạng JSON của MenuItemRequest)
    private sealed record ItemRequest(
        int CategoryId,
        string NameVi,
        decimal Price,
        decimal? OriginalPrice,
        string FullDescHtml,
        object Labels,
        string Status,
        string[] Allergens,
        int[] OptionGroupIds,
        bool AllowTakeaway = true,
        string ShortDesc = "Món ăn kèm canh");
}
