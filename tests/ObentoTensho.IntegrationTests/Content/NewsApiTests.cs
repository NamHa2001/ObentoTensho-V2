using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Content;

// docs/srs/17 §5, BR-CNT-01..03, BR-CNT-40..44: tin tức và trang tĩnh
[Collection(SqlServerFixture.CollectionName)]
public sealed class NewsApiTests : IDisposable
{
    private const string Admin = "/api/v1/admin";

    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public NewsApiTests(SqlServerFixture fixture)
    {
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

    private static string Etag(JsonElement item) => $"\"{item.GetProperty("version").GetString()}\"";

    private static NewsBody Body(string title, string status = "published", DateTime? publishedAt = null, string? slug = null, int? categoryId = null, string html = "<p>Nội dung</p>") =>
        new(title, slug, null, "Tóm tắt", html, categoryId, null, null, null, null, status, publishedAt);

    private sealed record NewsBody(string Title, string? Slug, Guid? ThumbnailImageId, string? Summary, string ContentHtml, int? CategoryId, string? AuthorDisplayName, string? MetaTitle, string? MetaDescription, Guid? OgImageId, string Status, DateTime? PublishedAtUtc);

    private async Task<JsonElement> CreateAsync(string token, NewsBody body)
    {
        var response = await _admin.SendAsync(HttpMethod.Post, $"{Admin}/news", token, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await AdminAuthApiClient.ReadDataAsync(response);
    }

    private Task<HttpResponseMessage> UpdateAsync(string token, JsonElement article, NewsBody body) =>
        _admin.SendAsync(HttpMethod.Put, $"{Admin}/news/{article.GetProperty("id").GetInt32()}", token, body, Etag(article));

    private Task<HttpStatusCode> PublicStatusAsync(string slug) => StatusAsync($"/api/v1/news/{slug}");

    private async Task<HttpStatusCode> StatusAsync(string path) => (await _http.GetAsync(path)).StatusCode;

    [Fact]
    public async Task Chi_bai_xuat_ban_da_toi_gio_moi_cong_khai_va_danh_sach_khong_co_noi_dung()
    {
        var editor = await _admin.SignInAsync(StaffRole.ContentEditor);
        var tag = Tag();

        var live = await CreateAsync(editor, Body($"Đăng {tag}", html: "<h2>Tiêu đề</h2><img src=\"/media/a.webp\" alt=\"a\" onerror=\"alert(1)\"><script>alert(1)</script>"));
        var draft = await CreateAsync(editor, Body($"Nháp {tag}", "draft"));
        var scheduled = await CreateAsync(editor, Body($"Hẹn {tag}", publishedAt: DateTime.UtcNow.AddHours(1)));
        var hidden = await CreateAsync(editor, Body($"Ẩn {tag}", "hidden", DateTime.UtcNow.AddHours(-1)));

        Assert.Equal(HttpStatusCode.NotFound, await PublicStatusAsync(draft.GetProperty("slug").GetString()!));
        Assert.Equal(HttpStatusCode.NotFound, await PublicStatusAsync(scheduled.GetProperty("slug").GetString()!));
        Assert.Equal(HttpStatusCode.NotFound, await PublicStatusAsync(hidden.GetProperty("slug").GetString()!));

        var body = await (await _http.GetAsync($"/api/v1/news?q={tag}")).Content.ReadFromJsonAsync<JsonElement>();
        var row = Assert.Single(body.GetProperty("data").EnumerateArray());
        Assert.Equal($"Đăng {tag}", row.GetProperty("title").GetString());
        Assert.Equal("Obento Tensho", row.GetProperty("authorDisplayName").GetString());
        Assert.False(row.TryGetProperty("contentHtml", out _));
        Assert.False(row.TryGetProperty("authorId", out _));

        var html = (await AdminAuthApiClient.ReadDataAsync(await _http.GetAsync($"/api/v1/news/{live.GetProperty("slug").GetString()}"))).GetProperty("contentHtml").GetString()!;
        Assert.Contains("<h2>Tiêu đề</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<img", html, StringComparison.Ordinal);
        Assert.DoesNotContain("onerror", html, StringComparison.Ordinal);
        Assert.DoesNotContain("script", html, StringComparison.Ordinal);

        // Sửa giờ đăng về quá khứ ⇒ hiện ngay, không cần job (BR-CNT-40)
        Assert.Equal(HttpStatusCode.OK, (await UpdateAsync(editor, scheduled, Body($"Hẹn {tag}", publishedAt: DateTime.UtcNow.AddMinutes(-1)))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, await PublicStatusAsync(scheduled.GetProperty("slug").GetString()!));
    }

    [Fact]
    public async Task Doi_slug_bai_da_dang_tra_301_con_bai_nhap_thi_khong()
    {
        var editor = await _admin.SignInAsync(StaffRole.Manager);
        var tag = Tag();
        var (first, second, third) = ($"tin-{tag}-1", $"tin-{tag}-2", $"tin-{tag}-3");

        var article = await CreateAsync(editor, Body($"Bài {tag}", slug: first));
        var updated = await AdminAuthApiClient.ReadDataAsync(await UpdateAsync(editor, article, Body($"Bài {tag}", slug: second)));

        var moved = await _http.GetAsync($"/api/v1/news/{first}");
        Assert.Equal(HttpStatusCode.MovedPermanently, moved.StatusCode);
        Assert.Equal($"/api/v1/news/{second}", moved.Headers.Location?.ToString());

        // Đổi tiếp: slug đầu tiên vẫn về địa chỉ mới nhất, không thành chuỗi chuyển hướng
        var latest = await AdminAuthApiClient.ReadDataAsync(await UpdateAsync(editor, updated, Body($"Bài {tag}", slug: third)));
        Assert.Equal($"/api/v1/news/{third}", (await _http.GetAsync($"/api/v1/news/{first}")).Headers.Location?.ToString());

        // Slug cũ của bài khác vẫn được giữ chỗ
        Assert.Equal(HttpStatusCode.Conflict, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/news", editor, Body($"Khác {tag}", slug: first))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/news", editor, Body($"Khác {tag}", slug: third))).StatusCode);

        // Quay lại slug cũ: địa chỉ đó hiện bài trực tiếp
        var back = await AdminAuthApiClient.ReadDataAsync(await UpdateAsync(editor, latest, Body($"Bài {tag}", slug: first)));
        Assert.Equal(HttpStatusCode.OK, await PublicStatusAsync(first));

        // Rời slug đó lần nữa: chuyển hướng cũ đã bỏ nên không trùng khóa
        Assert.Equal(HttpStatusCode.OK, (await UpdateAsync(editor, back, Body($"Bài {tag}", slug: second))).StatusCode);
        Assert.Equal($"/api/v1/news/{second}", (await _http.GetAsync($"/api/v1/news/{first}")).Headers.Location?.ToString());

        var draft = await CreateAsync(editor, Body($"Nháp {tag}", "draft", slug: $"nhap-{tag}"));
        Assert.Equal(HttpStatusCode.OK, (await UpdateAsync(editor, draft, Body($"Nháp {tag}", "draft", slug: $"nhap-moi-{tag}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, await PublicStatusAsync($"nhap-{tag}"));
        Assert.Equal(HttpStatusCode.Created, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/news", editor, Body($"Lại {tag}", "draft", slug: $"nhap-{tag}"))).StatusCode);

        Assert.Equal(HttpStatusCode.PreconditionRequired, (await _admin.SendAsync(HttpMethod.Put, $"{Admin}/news/{article.GetProperty("id").GetInt32()}", editor, Body("x"))).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await UpdateAsync(editor, article, Body("x"))).StatusCode);
    }

    [Fact]
    public async Task Xoa_mem_khoi_phuc_va_don_thung_rac_giu_danh_muc_con_bai()
    {
        var editor = await _admin.SignInAsync(StaffRole.ContentEditor);
        var tag = Tag();

        var category = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/news/categories", editor, new { name = $"Sự kiện {tag}", slug = (string?)null, isActive = true }));
        var categoryId = category.GetProperty("id").GetInt32();
        var categoryPath = $"{Admin}/news/categories/{categoryId}";
        var article = await CreateAsync(editor, Body($"Xóa {tag}", categoryId: categoryId));
        var (id, slug) = (article.GetProperty("id").GetInt32(), article.GetProperty("slug").GetString()!);

        var inCategory = await (await _http.GetAsync($"/api/v1/news?category={category.GetProperty("slug").GetString()}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, inCategory.GetProperty("data").GetArrayLength());
        Assert.Equal(HttpStatusCode.Conflict, (await _admin.SendAsync(HttpMethod.Delete, categoryPath, editor, null, Etag(category))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Delete, $"{Admin}/news/{id}", editor, null, Etag(article))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, await PublicStatusAsync(slug));
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.SendAsync(HttpMethod.Get, $"{Admin}/news/{id}", editor)).StatusCode);

        // Bài trong thùng rác vẫn giữ danh mục, tránh mất liên kết khi khôi phục
        Assert.Equal(HttpStatusCode.Conflict, (await _admin.SendAsync(HttpMethod.Delete, categoryPath, editor, null, Etag(category))).StatusCode);
        var trash = await (await _admin.SendAsync(HttpMethod.Get, $"{Admin}/news?trash=true&q={tag}", editor)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, trash.GetProperty("data").GetArrayLength());

        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/news/{id}/restore", editor)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, await PublicStatusAsync(slug));
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/news/{id}/restore", editor)).StatusCode);

        var current = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/news/{id}", editor));
        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Delete, $"{Admin}/news/{id}", editor, null, Etag(current))).StatusCode);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<INewsRepository>();
            await repository.PurgeTrashAsync(DateTime.UtcNow.AddDays(-30), CancellationToken.None);
            Assert.NotNull(await repository.FindAsync(id, includeDeleted: true, CancellationToken.None));
            Assert.True(await repository.PurgeTrashAsync(DateTime.UtcNow.AddMinutes(1), CancellationToken.None) >= 1);
        }

        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Delete, categoryPath, editor, null, Etag(category))).StatusCode);
    }

    [Fact]
    public async Task Trang_tinh_chi_nhan_slug_cho_phep_luu_phien_ban_va_khoi_phuc()
    {
        var editor = await _admin.SignInAsync(StaffRole.ContentEditor);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var tag = Tag();
        var path = $"{Admin}/pages/about";

        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, $"{Admin}/news", cashier)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Put, path, cashier, new { title = "x", contentHtml = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync("/api/v1/public/pages/hack"));
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.SendAsync(HttpMethod.Put, $"{Admin}/pages/hack", editor, new { title = "x", contentHtml = "x" })).StatusCode);

        var current = await _admin.SendAsync(HttpMethod.Get, path, editor);
        var etag = current.IsSuccessStatusCode ? Etag(await AdminAuthApiClient.ReadDataAsync(current)) : null;
        var first = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Put, path, editor, new { title = $"Một {tag}", contentHtml = "<h2>Một</h2><script>x</script>" }, etag));
        var revision = first.GetProperty("revision").GetInt32();
        Assert.Equal("<h2>Một</h2>", first.GetProperty("contentHtml").GetString());

        Assert.Equal(HttpStatusCode.PreconditionRequired, (await _admin.SendAsync(HttpMethod.Put, path, editor, new { title = "Thiếu", contentHtml = "x" })).StatusCode);
        var second = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Put, path, editor, new { title = $"Hai {tag}", contentHtml = "<p>Hai</p>" }, Etag(first)));
        Assert.Equal(revision + 1, second.GetProperty("revision").GetInt32());

        var revisions = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{path}/revisions", editor));
        Assert.Equal(revision + 1, revisions[0].GetProperty("revision").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.SendAsync(HttpMethod.Post, $"{path}/revisions/9999/restore", editor, null, Etag(second))).StatusCode);

        var restored = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{path}/revisions/{revision}/restore", editor, null, Etag(second)));
        Assert.Equal((revision + 2, $"Một {tag}"), (restored.GetProperty("revision").GetInt32(), restored.GetProperty("title").GetString()));

        var page = await AdminAuthApiClient.ReadDataAsync(await _http.GetAsync("/api/v1/public/pages/about"));
        Assert.Equal("<h2>Một</h2>", page.GetProperty("contentHtml").GetString());

        var list = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/pages", editor));
        Assert.Equal(6, list.GetArrayLength());
    }
}
