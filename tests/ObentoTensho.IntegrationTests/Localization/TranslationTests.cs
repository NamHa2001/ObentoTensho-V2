using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Settings.Domain;
using ObentoTensho.Core.Settings.Services;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;

namespace ObentoTensho.IntegrationTests.Localization;

// docs/srs/22 §7: chỉ bản đã duyệt và còn khớp bản Việt mới hiển thị; thiếu thì tiếng Việt kèm cờ;
// ngôn ngữ chưa bật hoặc lạ ⇒ tiếng Việt; chỉ bật ngôn ngữ khi đủ bản dịch
[Collection(SqlServerFixture.CollectionName)]
public sealed class TranslationTests : IDisposable
{
    private const string Admin = "/api/v1/admin";
    private const string Translations = "/api/v1/admin/translations";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public TranslationTests(SqlServerFixture fixture)
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

    private async Task<(int Id, string Slug)> PromotionAsync(string token, string tag)
    {
        var body = new
        {
            title = $"Giảm giá {tag}",
            slug = $"km-{tag}",
            descriptionHtml = "<p>Giảm 10% cho cơm gà</p>",
            startDate = OrderApiClient.Date(TestSchedule.TodayVn),
            endDate = OrderApiClient.Date(TestSchedule.TodayVn.AddDays(5)),
            publicTeaser = false,
            isActive = true,
        };
        var created = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/promotions", token, body));
        return (created.GetProperty("id").GetInt32(), created.GetProperty("slug").GetString()!);
    }

    // docs/srs/22 §4.2: tìm tin tức theo tiêu đề bản dịch đã duyệt; bản chưa duyệt hoặc đã lỗi thời thì không
    [Fact]
    public async Task Tim_tin_tuc_theo_tieu_de_ban_dich_da_duyet()
    {
        var editor = await _admin.SignInAsync(StaffRole.ContentEditor);
        var (tag, english, japanese) = (Guid.NewGuid().ToString("N")[..8], $"blossom{Guid.NewGuid():N}"[..16], $"pending{Guid.NewGuid():N}"[..16]);
        object Article(string title) => new
        {
            title, slug = (string?)null, thumbnailImageId = (Guid?)null, summary = "Tóm tắt", contentHtml = "<p>Nội dung</p>",
            categoryId = (int?)null, authorDisplayName = (string?)null, metaTitle = (string?)null, metaDescription = (string?)null, ogImageId = (Guid?)null,
            status = "published", publishedAtUtc = (DateTime?)null,
        };
        var created = await _admin.SendAsync(HttpMethod.Post, $"{Admin}/news", editor, Article($"Hội hoa {tag}"));
        var id = (await AdminAuthApiClient.ReadDataAsync(created)).GetProperty("id").GetInt32();
        var key = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        async Task<int> FoundAsync(string q) =>
            (await (await _http.GetAsync($"/api/v1/news?q={q}")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetArrayLength();

        Assert.Equal(HttpStatusCode.OK, (await TranslateAsync(editor, "news", key, "en", Text($"Cherry {english}", "<p>Body</p>"))).StatusCode);
        Assert.Equal(1, await FoundAsync(english));
        Assert.Equal(HttpStatusCode.OK, (await TranslateAsync(editor, "news", key, "ja", Text($"桜 {japanese}", "<p>本文</p>", approve: false))).StatusCode);
        Assert.Equal(1, await FoundAsync(english.ToUpperInvariant()));
        Assert.Equal(1, await FoundAsync($"hoi hoa {tag}"));
        Assert.Equal(0, await FoundAsync(japanese));

        // Lưu lại bản Việt không đổi gì: bản dịch vẫn hợp lệ nên vẫn tìm được
        var etag = (await _admin.SendAsync(HttpMethod.Get, $"{Admin}/news/{id}", editor)).Headers.ETag?.Tag;
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Put, $"{Admin}/news/{id}", editor, Article($"Hội hoa {tag}"), etag)).StatusCode);
        Assert.Equal(1, await FoundAsync(english));

        // Sửa bản Việt ⇒ bản dịch lỗi thời, không còn tìm theo tiêu đề tiếng Anh
        etag = (await _admin.SendAsync(HttpMethod.Get, $"{Admin}/news/{id}", editor)).Headers.ETag?.Tag;
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Put, $"{Admin}/news/{id}", editor, Article($"Hội hoa mới {tag}"), etag)).StatusCode);
        Assert.Equal(0, await FoundAsync(english));
        Assert.Equal(1, await FoundAsync($"hoa moi {tag}"));
    }

    private Task<HttpResponseMessage> TranslateAsync(string token, string kind, string key, string lang, object body) =>
        _admin.SendAsync(HttpMethod.Put, $"{Translations}/{kind}/{key}/{lang}", token, body);

    private static object Text(string title, string? body = null, bool approve = true, string? summary = null) =>
        new { title, summary, bodyHtml = body, ctaText = (string?)null, metaTitle = (string?)null, metaDescription = (string?)null, approve };

    private async Task<JsonElement> PublicAsync(string path) => await AdminAuthApiClient.ReadDataAsync(await _http.GetAsync(path));

    private static (string Title, string Lang, bool Fallback) Shown(JsonElement item) =>
        (item.GetProperty("title").GetString()!, item.GetProperty("contentLang").GetString()!, item.GetProperty("isFallback").GetBoolean());

    [Fact]
    public async Task Ban_nhap_khong_hien_ban_duyet_hien_va_sua_ban_Viet_thi_ban_dich_loi_thoi()
    {
        var editor = await _admin.SignInAsync(StaffRole.ContentEditor);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var tag = Tag();
        var (id, slug) = await PromotionAsync(manager, tag);
        var key = id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(HttpStatusCode.OK, (await TranslateAsync(editor, "promotion", key, "en", Text($"Sale {tag}", "<p>10% off</p>", approve: false))).StatusCode);
        Assert.Equal(($"Giảm giá {tag}", "vi", true), Shown(await PublicAsync($"/api/v1/promotions/{slug}?lang=en")));

        Assert.Equal(HttpStatusCode.OK, (await TranslateAsync(editor, "promotion", key, "en", Text($"Sale {tag}", "<p onclick=\"x()\">10% off</p><script>x</script>"))).StatusCode);
        var shown = await PublicAsync($"/api/v1/promotions/{slug}?lang=en");
        Assert.Equal(($"Sale {tag}", "en", false), Shown(shown));
        Assert.Equal("<p>10% off</p>", shown.GetProperty("descriptionHtml").GetString());
        Assert.Equal(($"Giảm giá {tag}", "vi", false), Shown(await PublicAsync($"/api/v1/promotions/{slug}")));

        // Sửa bản Việt ⇒ bản dịch lỗi thời, khách en thấy tiếng Việt
        var current = await _admin.SendAsync(HttpMethod.Get, $"{Admin}/promotions/{id}", manager);
        var edit = new
        {
            title = $"Giảm giá {tag}",
            slug,
            descriptionHtml = "<p>Giảm 15% cho cơm gà</p>",
            startDate = OrderApiClient.Date(TestSchedule.TodayVn),
            endDate = OrderApiClient.Date(TestSchedule.TodayVn.AddDays(5)),
            publicTeaser = false,
            isActive = true,
        };
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Put, $"{Admin}/promotions/{id}", manager, edit, current.Headers.ETag?.Tag)).StatusCode);

        Assert.Equal(($"Giảm giá {tag}", "vi", true), Shown(await PublicAsync($"/api/v1/promotions/{slug}?lang=en")));
        var list = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Translations}/promotion/{key}", editor));
        Assert.True(Assert.Single(list.EnumerateArray()).GetProperty("outdated").GetBoolean());
    }

    [Fact]
    public async Task Ngon_ngu_la_hoac_chua_bat_thi_dung_tieng_Viet()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var tag = Tag();
        var (id, slug) = await PromotionAsync(manager, tag);
        var key = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(HttpStatusCode.OK, (await TranslateAsync(manager, "promotion", key, "ja", Text($"セール {tag}", "<p>10%オフ</p>"))).StatusCode);

        Assert.Equal(($"セール {tag}", "ja", false), Shown(await PublicAsync($"/api/v1/promotions/{slug}?lang=JA")));
        Assert.Equal(($"Giảm giá {tag}", "vi", false), Shown(await PublicAsync($"/api/v1/promotions/{slug}?lang=xx")));
        Assert.Equal(($"Giảm giá {tag}", "vi", false), Shown(await PublicAsync($"/api/v1/promotions/{slug}?lang=../en")));

        await using (var db = _fixture.CreateContext())
        {
            await db.SystemSettings.Where(s => s.Key == SettingKeys.I18nEnabledLocales).ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, "vi,en"));
        }

        try
        {
            await RefreshSettingsAsync();
            Assert.Equal(($"Giảm giá {tag}", "vi", false), Shown(await PublicAsync($"/api/v1/promotions/{slug}?lang=ja")));
        }
        finally
        {
            await RestoreLocalesAsync();
        }
    }

    [Fact]
    public async Task Kiem_du_lieu_ban_dich_va_phan_quyen()
    {
        var editor = await _admin.SignInAsync(StaffRole.ContentEditor);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var tag = Tag();
        var (id, _) = await PromotionAsync(manager, tag);
        var key = id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(HttpStatusCode.BadRequest, (await TranslateAsync(editor, "promotion", key, "en", Text("Sale \u202Eevil"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await TranslateAsync(editor, "promotion", key, "en", Text(new string('x', 201)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await TranslateAsync(editor, "promotion", key, "en", Text("  "))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await TranslateAsync(editor, "promotion", "999999999", "en", Text("Sale"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await TranslateAsync(editor, "static_page", "hack", "en", Text("Hack", "<p>x</p>"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await TranslateAsync(editor, "promotion", key, "fr", Text("Promo"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TranslateAsync(cashier, "promotion", key, "en", Text("Sale"))).StatusCode);

        // §4.3: ContentEditor dịch được cả thực đơn
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var menuBody = new { name = "Egg", shortDesc = (string?)null, fullDescHtml = (string?)null, approve = false };
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Put, $"{Admin}/menu/options/{menu.Egg}/translations/en", editor, menuBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Put, $"{Admin}/menu/options/{menu.Egg}/translations/en", cashier, menuBody)).StatusCode);

        // Trường bản Việt không có (khuyến mãi không có tóm tắt) thì bị bỏ, không lưu
        var saved = await AdminAuthApiClient.ReadDataAsync(await TranslateAsync(editor, "promotion", key, "en", Text("Sale", summary: "ignored")));
        Assert.Equal(JsonValueKind.Null, saved.GetProperty("summary").ValueKind);
    }

    [Fact]
    public async Task Trang_tinh_va_tin_tuc_dich_theo_tung_truong()
    {
        var editor = await _admin.SignInAsync(StaffRole.ContentEditor);
        var tag = Tag();
        var current = await _admin.SendAsync(HttpMethod.Get, $"{Admin}/pages/privacy", editor);
        var etag = current.IsSuccessStatusCode ? $"\"{(await AdminAuthApiClient.ReadDataAsync(current)).GetProperty("version").GetString()}\"" : null;
        Assert.True((await _admin.SendAsync(HttpMethod.Put, $"{Admin}/pages/privacy", editor, new { title = $"Chính sách {tag}", contentHtml = "<p>Bản Việt có giá trị</p>" }, etag)).IsSuccessStatusCode);

        // Trang tĩnh bắt buộc dịch cả nội dung
        Assert.Equal(HttpStatusCode.BadRequest, (await TranslateAsync(editor, "static_page", "privacy", "en", Text("Privacy"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TranslateAsync(editor, "static_page", "privacy", "en", Text($"Privacy {tag}", "<p>Reference translation</p>"))).StatusCode);
        var page = await PublicAsync("/api/v1/public/pages/privacy?lang=en");
        Assert.Equal(($"Privacy {tag}", "en", false), Shown(page));
        Assert.Equal("<p>Reference translation</p>", page.GetProperty("contentHtml").GetString());

        // Tin tức: bản dịch thiếu tóm tắt ⇒ tóm tắt tiếng Việt, cờ dự phòng bật
        var article = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/news", editor, new
        {
            title = $"Tin {tag}", slug = (string?)null, thumbnailImageId = (Guid?)null, summary = "Tóm tắt", contentHtml = "<p>Nội dung</p>",
            categoryId = (int?)null, authorDisplayName = (string?)null, metaTitle = (string?)null, metaDescription = (string?)null, ogImageId = (Guid?)null,
            status = "published", publishedAtUtc = (DateTime?)null,
        }));
        var newsKey = article.GetProperty("id").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(HttpStatusCode.OK, (await TranslateAsync(editor, "news", newsKey, "en", Text($"News {tag}", "<p>Body</p>"))).StatusCode);

        var row = Assert.Single((await (await _http.GetAsync($"/api/v1/news?q={tag}&lang=en")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").EnumerateArray());
        Assert.Equal(($"News {tag}", "en", true), Shown(row));
        Assert.Equal("Tóm tắt", row.GetProperty("summary").GetString());
        var detail = await PublicAsync($"/api/v1/news/{article.GetProperty("slug").GetString()}?lang=en");
        Assert.Equal("<p>Body</p>", detail.GetProperty("contentHtml").GetString());
    }

    [Fact]
    public async Task Chi_bat_ngon_ngu_khi_mon_dang_ban_da_co_ban_dich_duyet()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        await using (var db = _fixture.CreateContext())
        {
            await db.SystemSettings.Where(s => s.Key == SettingKeys.I18nEnabledLocales).ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, "vi"));
        }

        try
        {
            await RefreshSettingsAsync();
            var enable = await _admin.SendAsync(HttpMethod.Put, $"{Admin}/settings", manager, new { values = new Dictionary<string, string>(StringComparer.Ordinal) { [SettingKeys.I18nEnabledLocales] = "vi,en" } });
            Assert.Equal(HttpStatusCode.Conflict, enable.StatusCode);
            var problem = await enable.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(ErrorCodes.LocaleNotReady, problem.GetProperty("errorCode").GetString());
            var missing = problem.GetProperty("errors").GetProperty(SettingKeys.I18nEnabledLocales).EnumerateArray().Select(e => e.GetString()).ToArray();
            // Danh sách trong lỗi bị giới hạn số dòng; danh sách đầy đủ ở màn hình tiến độ dịch
            Assert.NotEmpty(missing);
            Assert.All(missing, m => Assert.StartsWith("en:", m, StringComparison.Ordinal));

            var removeVi = await _admin.SendAsync(HttpMethod.Put, $"{Admin}/settings", manager, new { values = new Dictionary<string, string>(StringComparer.Ordinal) { [SettingKeys.I18nEnabledLocales] = "en" } });
            Assert.Equal(HttpStatusCode.BadRequest, removeVi.StatusCode);

            // Bản nháp vẫn chặn; dịch và duyệt món đó ⇒ không còn trong danh sách chặn
            var before = await ProgressAsync(manager);
            Assert.Contains($"menu_item:{menu.Tea}", before.Blocking, StringComparer.Ordinal);
            Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Put, $"{Admin}/menu/items/{menu.Tea}/translations/en", manager, new { name = "Green tea", shortDesc = (string?)null, fullDescHtml = (string?)null, approve = false })).StatusCode);
            var drafted = await ProgressAsync(manager);
            Assert.Contains($"menu_item:{menu.Tea}", drafted.Blocking, StringComparer.Ordinal);
            Assert.Equal(Count(before, k => k.Draft) + 1, Count(drafted, k => k.Draft));
            Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Put, $"{Admin}/menu/items/{menu.Tea}/translations/en", manager, new { name = "Green tea", shortDesc = (string?)null, fullDescHtml = (string?)null, approve = true })).StatusCode);
            var after = await ProgressAsync(manager);
            Assert.DoesNotContain($"menu_item:{menu.Tea}", after.Blocking, StringComparer.Ordinal);
            Assert.False(after.Enabled);
            Assert.True(Approved(after) > Approved(before));
        }
        finally
        {
            await RestoreLocalesAsync();
        }
    }

    [Fact]
    public async Task Thuc_don_khong_hien_ngon_ngu_chua_bat()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Put, $"{Admin}/menu/items/{menu.Rice}/translations/ja", manager, new { name = "照り焼き丼", shortDesc = (string?)null, fullDescHtml = (string?)null, approve = true })).StatusCode);
        var slug = await SlugAsync(menu.Rice);

        Assert.Equal("ja", (await PublicAsync($"/api/v1/menu/items/{slug}?lang=ja")).GetProperty("item").GetProperty("contentLang").GetString());
        await using (var db = _fixture.CreateContext())
        {
            await db.SystemSettings.Where(s => s.Key == SettingKeys.I18nEnabledLocales).ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, "vi,en"));
        }

        try
        {
            await RefreshSettingsAsync();
            Assert.Equal("vi", (await PublicAsync($"/api/v1/menu/items/{slug}?lang=ja")).GetProperty("item").GetProperty("contentLang").GetString());
        }
        finally
        {
            await RestoreLocalesAsync();
        }
    }

    private static int Approved(Progress progress) => Count(progress, k => k.Approved);

    private static int Count(Progress progress, Func<KindCount, int> pick) =>
        pick(progress.Kinds.Single(k => string.Equals(k.Kind, "menu_item", StringComparison.Ordinal)));

    private async Task<string> SlugAsync(int itemId)
    {
        await using var db = _fixture.CreateContext();
        return await db.MenuItems.Where(i => i.Id == itemId).Select(i => i.Slug).SingleAsync();
    }

    private async Task<Progress> ProgressAsync(string token)
    {
        var data = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Translations}/progress?lang=en", token));
        return new Progress(
            data.GetProperty("enabled").GetBoolean(),
            data.GetProperty("blocking").EnumerateArray().Select(e => e.GetString()!).ToArray(),
            data.GetProperty("kinds").EnumerateArray().Select(k => new KindCount(k.GetProperty("kind").GetString()!, k.GetProperty("approved").GetInt32(), k.GetProperty("draft").GetInt32())).ToArray());
    }

    private async Task RefreshSettingsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SettingsSeeder>().RefreshAsync(CancellationToken.None);
    }

    private async Task RestoreLocalesAsync()
    {
        await using (var db = _fixture.CreateContext())
        {
            await db.SystemSettings.Where(s => s.Key == SettingKeys.I18nEnabledLocales).ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, SqlServerFixture.EnabledLocales));
        }

        await RefreshSettingsAsync();
    }

    private sealed record Progress(bool Enabled, string[] Blocking, KindCount[] Kinds);

    private sealed record KindCount(string Kind, int Approved, int Draft);
}
