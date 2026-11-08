using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetVips;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;

namespace ObentoTensho.IntegrationTests.Menu;

// BR-MNU-06/12 (ảnh món), docs/srs/08 (ảnh danh mục, banner khuyến mãi), BR-MNU-23 (chỉ ẩn tùy chọn đã có đơn),
// BR-CNT-41 (slug cũ 301), docs/srs/15 §4 (đánh giá theo món)
[Collection(SqlServerFixture.CollectionName)]
public sealed class MenuMediaTests : IDisposable
{
    private const string Admin = "/api/v1/admin";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public MenuMediaTests(SqlServerFixture fixture)
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

    private static string Tag() => Guid.NewGuid().ToString("N")[..10];

    private static byte[] Jpeg()
    {
        using var noise = Image.Gaussnoise(400, 300, mean: Random.Shared.Next(60, 200), sigma: 30);
        using var band = noise.Cast(Enums.BandFormat.Uchar);
        using var rgb = band.Bandjoin(band, band);
        using var srgb = rgb.Copy(interpretation: Enums.Interpretation.Srgb);
        return srgb.JpegsaveBuffer(q: 60);
    }

    private async Task<Guid> UploadAsync(string token)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Jpeg());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "mon.jpg");
        form.Add(new StringContent("Món ăn"), "altText");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Admin}/images") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (await AdminAuthApiClient.ReadDataAsync(await _http.SendAsync(request))).GetProperty("id").GetGuid();
    }

    private async Task<string> SlugOfAsync(int itemId)
    {
        await using var db = _fixture.CreateContext();
        return await db.MenuItems.Where(i => i.Id == itemId).Select(i => i.Slug).SingleAsync();
    }

    private async Task<JsonElement> PublicAsync(string path) => await AdminAuthApiClient.ReadDataAsync(await _http.GetAsync(path));

    [Fact]
    public async Task Anh_mon_thay_ca_bo_toi_da_4_anh_phu_va_hien_o_trang_khach()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var editor = await _admin.SignInAsync(StaffRole.ContentEditor);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var images = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            images.Add(await UploadAsync(manager));
        }

        var path = $"{Admin}/menu/items/{menu.Rice}/images";
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Put, path, manager, new { mainImageId = images[0], imageIds = images.Skip(1).Take(5) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Put, path, manager, new { mainImageId = images[0], imageIds = new[] { images[1], images[1] } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Put, path, manager, new { mainImageId = Guid.NewGuid(), imageIds = Array.Empty<Guid>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Put, path, manager, new { mainImageId = (Guid?)null, imageIds = new[] { Guid.NewGuid() } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Put, path, editor, new { mainImageId = images[0], imageIds = Array.Empty<Guid>() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.SendAsync(HttpMethod.Put, $"{Admin}/menu/items/999999999/images", manager, new { mainImageId = images[0], imageIds = Array.Empty<Guid>() })).StatusCode);

        var saved = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Put, path, manager, new { mainImageId = images[0], imageIds = new[] { images[2], images[1] } }));
        Assert.Equal(images[0], saved.GetProperty("mainImage").GetProperty("id").GetGuid());
        Assert.Equal([images[2], images[1]], saved.GetProperty("images").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));

        var slug = await SlugOfAsync(menu.Rice);
        var detail = await PublicAsync($"/api/v1/menu/items/{slug}");
        Assert.Equal(images[0], detail.GetProperty("item").GetProperty("image").GetProperty("id").GetGuid());
        Assert.Equal(2, detail.GetProperty("images").GetArrayLength());

        var reread = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, path, manager));
        Assert.Equal([images[2], images[1]], reread.GetProperty("images").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));

        // Ảnh chưa xử lý xong không lên trang khách
        await using (var db = _fixture.CreateContext())
        {
            await db.Images.Where(i => i.Id == images[4]).ExecuteUpdateAsync(u => u.SetProperty(i => i.Status, ImageStatus.Processing));
        }

        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Put, path, manager, new { mainImageId = images[4], imageIds = new[] { images[2] } })).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await PublicAsync($"/api/v1/menu/items/{slug}")).GetProperty("item").GetProperty("image").ValueKind);

        // Thay bộ mới: bỏ ảnh chính, còn một ảnh phụ
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Put, path, manager, new { mainImageId = (Guid?)null, imageIds = new[] { images[3] } })).StatusCode);
        detail = await PublicAsync($"/api/v1/menu/items/{slug}");
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("item").GetProperty("image").ValueKind);
        Assert.Equal(images[3], Assert.Single(detail.GetProperty("images").EnumerateArray()).GetProperty("id").GetGuid());

        var entityId = menu.Rice.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await using var check = _fixture.CreateContext();
        Assert.True(await check.AuditLogs.AnyAsync(a => a.Action == "menu.menu_item.images_updated" && a.EntityId == entityId));
    }

    [Fact]
    public async Task Anh_danh_muc_va_banner_khuyen_mai_chi_nhan_anh_da_tai()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var tag = Tag();
        var image = await UploadAsync(manager);

        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/menu/categories", manager, new { name = $"Sai {tag}", slug = (string?)null, isActive = true, imageId = Guid.NewGuid() })).StatusCode);
        var category = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/menu/categories", manager, new { name = $"Cơm {tag}", slug = (string?)null, isActive = true, imageId = image }));
        Assert.Equal(image, category.GetProperty("imageId").GetGuid());

        // Danh mục chỉ hiện khi có món đang bán
        await using (var db = _fixture.CreateContext())
        {
            db.MenuItems.Add(new MenuItem { CategoryId = category.GetProperty("id").GetInt32(), NameVi = $"Món {tag}", Slug = $"mon-{tag}", NameSearch = tag, Price = 50_000, CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var categories = await PublicAsync("/api/v1/menu/categories");
        var shown = categories.EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == category.GetProperty("id").GetInt32());
        Assert.Equal(image, shown.GetProperty("image").GetProperty("id").GetGuid());

        object Promotion(Guid? banner) => new
        {
            title = $"KM {tag}", slug = $"km-{tag}", descriptionHtml = (string?)null,
            startDate = OrderApiClient.Date(TestSchedule.TodayVn), endDate = OrderApiClient.Date(TestSchedule.TodayVn.AddDays(3)),
            publicTeaser = false, isActive = true, bannerImageId = banner,
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/promotions", manager, Promotion(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/promotions", manager, Promotion(image))).StatusCode);
        Assert.Equal(image, (await PublicAsync($"/api/v1/promotions/km-{tag}")).GetProperty("bannerImage").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Xoa_tuy_chon_da_co_trong_don_thi_chi_an()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var orders = new OrderApiClient(_http);
        Assert.Equal(HttpStatusCode.Created, (await orders.CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Rice, 1, menu.Large)]))).StatusCode);

        int unused;
        await using (var db = _fixture.CreateContext())
        {
            var groupId = await db.Options.Where(o => o.Id == menu.Large).Select(o => o.GroupId).SingleAsync();
            var option = new MenuOption { GroupId = groupId, Name = "Vừa", PriceDelta = 5_000 };
            db.Options.Add(option);
            await db.SaveChangesAsync();
            unused = option.Id;
        }

        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Delete, $"{Admin}/menu/options/{menu.Large}", manager)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Delete, $"{Admin}/menu/options/{unused}", manager)).StatusCode);

        await using var check = _fixture.CreateContext();
        Assert.Equal(SaleStatus.Hidden, await check.Options.Where(o => o.Id == menu.Large).Select(o => o.Status).SingleAsync());
        Assert.False(await check.Options.AnyAsync(o => o.Id == unused));
        var detail = await PublicAsync($"/api/v1/menu/items/{await SlugOfAsync(menu.Rice)}");
        var shownOptions = detail.GetProperty("optionGroups")[0].GetProperty("options").EnumerateArray().Select(o => o.GetProperty("id").GetInt32());
        Assert.DoesNotContain(menu.Large, shownOptions);
    }

    [Fact]
    public async Task Doi_slug_mon_va_khuyen_mai_thi_slug_cu_tra_301()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var tag = Tag();
        var oldSlug = await SlugOfAsync(menu.Tea);

        var current = await _admin.SendAsync(HttpMethod.Get, $"{Admin}/menu/items/{menu.Tea}", manager);
        var item = await AdminAuthApiClient.ReadDataAsync(current);
        var body = new
        {
            categoryId = item.GetProperty("categoryId").GetInt32(),
            nameVi = item.GetProperty("nameVi").GetString(),
            slug = $"tra-moi-{tag}",
            shortDesc = (string?)null,
            fullDescHtml = (string?)null,
            price = item.GetProperty("price").GetDecimal(),
            originalPrice = (decimal?)null,
            labels = item.GetProperty("labels"),
            allowTakeaway = true,
            status = "available",
            allergens = Array.Empty<string>(),
            optionGroupIds = item.GetProperty("optionGroupIds").EnumerateArray().Select(g => g.GetInt32()).ToArray(),
        };
        var etag = $"\"{item.GetProperty("version").GetString()}\"";
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Put, $"{Admin}/menu/items/{menu.Tea}", manager, body, etag)).StatusCode);

        var moved = await _http.GetAsync($"/api/v1/menu/items/{oldSlug}?lang=en");
        Assert.Equal(HttpStatusCode.MovedPermanently, moved.StatusCode);
        Assert.Equal($"/api/v1/menu/items/tra-moi-{tag}?lang=en", moved.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/v1/menu/items/khong-co-{tag}")).StatusCode);

        // Khuyến mãi
        var created = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/promotions", manager, new
        {
            title = $"KM {tag}", slug = $"km-cu-{tag}", descriptionHtml = (string?)null,
            startDate = OrderApiClient.Date(TestSchedule.TodayVn), endDate = OrderApiClient.Date(TestSchedule.TodayVn.AddDays(3)),
            publicTeaser = false, isActive = true,
        }));
        var id = created.GetProperty("id").GetInt32();
        var promoEtag = (await _admin.SendAsync(HttpMethod.Get, $"{Admin}/promotions/{id}", manager)).Headers.ETag?.Tag;
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Put, $"{Admin}/promotions/{id}", manager, new
        {
            title = $"KM {tag}", slug = $"km-moi-{tag}", descriptionHtml = (string?)null,
            startDate = OrderApiClient.Date(TestSchedule.TodayVn), endDate = OrderApiClient.Date(TestSchedule.TodayVn.AddDays(3)),
            publicTeaser = false, isActive = true,
        }, promoEtag)).StatusCode);

        var promoMoved = await _http.GetAsync($"/api/v1/promotions/km-cu-{tag}");
        Assert.Equal(HttpStatusCode.MovedPermanently, promoMoved.StatusCode);
        Assert.Equal($"/api/v1/promotions/km-moi-{tag}", promoMoved.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Danh_gia_theo_mon_chi_cho_mon_dang_ban()
    {
        var menu = await OrderTestMenu.CreateAsync(_fixture);

        var response = await _http.GetAsync($"/api/v1/menu/items/{await SlugOfAsync(menu.Rice)}/reviews");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("meta").GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/v1/menu/items/khong-co-{Tag()}/reviews")).StatusCode);
    }
}
