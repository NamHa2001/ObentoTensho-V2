using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetVips;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Media.Services;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using Image = NetVips.Image;

namespace ObentoTensho.IntegrationTests.Content;

// docs/srs/04 §2 và docs/srs/17 §7: tải ảnh, banner hẹn ngày, gallery
[Collection(SqlServerFixture.CollectionName)]
public sealed class ContentApiTests : IDisposable
{
    private const string Admin = "/api/v1/admin";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public ContentApiTests(SqlServerFixture fixture)
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

    private static DateOnly Today => TestSchedule.TodayVn;

    [Fact]
    public async Task Tai_anh_sinh_bien_the_phuc_vu_duoc_va_trung_noi_dung_khong_luu_lai()
    {
        var editor = await _admin.SignInAsync(StaffRole.ContentEditor);
        var cashier = await _admin.SignInAsync(StaffRole.Cashier);
        var jpeg = Jpeg(1100, 700);

        var created = await UploadAsync(editor, jpeg);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var image = await AdminAuthApiClient.ReadDataAsync(created);
        Assert.Equal("ready", image.GetProperty("status").GetString());
        var variants = image.GetProperty("variants");
        Assert.Equal(3, variants.GetArrayLength());

        var served = await _http.GetAsync(new Uri(variants[0].GetProperty("url").GetString()!, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.True(served.Headers.CacheControl?.Public);
        Assert.Contains("immutable", served.Headers.GetValues("Cache-Control").Single(), StringComparison.Ordinal);

        var again = await AdminAuthApiClient.ReadDataAsync(await UploadAsync(editor, jpeg));
        Assert.Equal(image.GetProperty("id").GetGuid(), again.GetProperty("id").GetGuid());

        Assert.Equal(ErrorCodes.FileInvalid, await ErrorOf(UploadAsync(editor, "khong phai anh"u8.ToArray())));
        Assert.Equal(ErrorCodes.ValidationFailed, await ErrorOf(UploadAsync(editor, jpeg, altText: " ")));
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(cashier, jpeg)).StatusCode);
    }

    [Fact]
    public async Task Anh_lon_xu_ly_nen_bang_viec_dinh_ky()
    {
        var editor = await _admin.SignInAsync(StaffRole.Manager);

        // 4.000 × 3.500 = 14 MP, vượt ngưỡng xử lý ngay 12 MP
        var image = await AdminAuthApiClient.ReadDataAsync(await UploadAsync(editor, Jpeg(4000, 3500)));
        Assert.Equal("processing", image.GetProperty("status").GetString());
        Assert.Equal(0, image.GetProperty("variants").GetArrayLength());

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<ImageService>().ProcessPendingAsync(CancellationToken.None) >= 1);
        }

        var ready = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/images/{image.GetProperty("id").GetGuid()}", editor));
        Assert.Equal(("ready", 5), (ready.GetProperty("status").GetString(), ready.GetProperty("variants").GetArrayLength()));
    }

    [Fact]
    public async Task Banner_hien_dung_ngay_va_lien_ket_la_bi_tu_choi()
    {
        var editor = await _admin.SignInAsync(StaffRole.ContentEditor);
        var imageId = (await AdminAuthApiClient.ReadDataAsync(await UploadAsync(editor, Jpeg(1700, 700)))).GetProperty("id").GetGuid();
        var tag = Guid.NewGuid().ToString("N")[..8];

        var now = await CreateBannerAsync(editor, imageId, $"Hôm nay {tag}", Today, Today);
        await CreateBannerAsync(editor, imageId, $"Tết {tag}", Today.AddDays(1), null);
        await CreateBannerAsync(editor, imageId, $"Cũ {tag}", null, Today.AddDays(-1));

        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/banners", editor, Banner(imageId, "Xấu", null, null, "https://evil.test/x"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/banners", editor, Banner(Guid.NewGuid(), "Không ảnh", null, null))).StatusCode);

        var titles = (await PublicAsync("/api/v1/banners")).EnumerateArray().Select(b => b.GetProperty("title").GetString()!).Where(t => t.EndsWith(tag, StringComparison.Ordinal)).ToArray();
        Assert.Equal([$"Hôm nay {tag}"], titles, StringComparer.Ordinal);

        // Tắt ⇒ biến mất ngay (cache đã xóa)
        var etag = $"\"{now.GetProperty("version").GetString()}\"";
        var off = Banner(imageId, $"Hôm nay {tag}", Today, Today) with { IsActive = false };
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Put, $"{Admin}/banners/{now.GetProperty("id").GetInt32()}", editor, off, etag)).StatusCode);
        Assert.DoesNotContain(tag, (await PublicAsync("/api/v1/banners")).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sap_thu_tu_phai_du_danh_sach_va_gallery_chi_nhan_video_cho_phep()
    {
        var editor = await _admin.SignInAsync(StaffRole.Manager);
        var imageId = (await AdminAuthApiClient.ReadDataAsync(await UploadAsync(editor, Jpeg(500, 500)))).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/gallery", editor, Video("https://evil.test/v.mp4"))).StatusCode);
        var video = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/gallery", editor, Video("https://youtu.be/dQw4w9WgXcQ")));
        var photo = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/gallery", editor, new { mediaType = "image", imageId, videoUrl = (string?)null, category = "food", caption = "Cơm gà", isActive = true }));

        var food = await PublicAsync("/api/v1/gallery?category=food");
        Assert.Contains(food.EnumerateArray(), g => g.GetProperty("id").GetInt32() == photo.GetProperty("id").GetInt32());
        var events = await PublicAsync("/api/v1/gallery?category=event");
        var embedded = events.EnumerateArray().Single(g => g.GetProperty("id").GetInt32() == video.GetProperty("id").GetInt32());
        Assert.Equal("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", embedded.GetProperty("embedUrl").GetString());

        var all = (await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/gallery", editor))).EnumerateArray().Select(g => g.GetProperty("id").GetInt32()).ToArray();
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Patch, $"{Admin}/gallery/reorder", editor, new { ids = all[1..] })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Patch, $"{Admin}/gallery/reorder", editor, new { ids = all.Reverse().ToArray() })).StatusCode);

        var first = (await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/gallery", editor)))[0].GetProperty("id").GetInt32();
        Assert.Equal(all[^1], first);
    }

    private static object Video(string url) => new { mediaType = "video", imageId = (Guid?)null, videoUrl = url, category = "event", caption = (string?)null, isActive = true };

    private static BannerBody Banner(Guid imageId, string title, DateOnly? from, DateOnly? to, string? link = "/menu") =>
        new(imageId, null, title, "Phụ đề", "Xem ngay", link, from, to, true);

    private sealed record BannerBody(Guid ImageId, Guid? ImageMobileId, string Title, string? Subtitle, string? CtaText, string? CtaLink, DateOnly? ShowFromDate, DateOnly? ShowToDate, bool IsActive);

    private async Task<JsonElement> CreateBannerAsync(string token, Guid imageId, string title, DateOnly? from, DateOnly? to)
    {
        var response = await _admin.SendAsync(HttpMethod.Post, $"{Admin}/banners", token, Banner(imageId, title, from, to));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await AdminAuthApiClient.ReadDataAsync(response);
    }

    // Ảnh nhiễu mỗi lần một khác nên không bị coi là trùng với lần chạy trước
    private static byte[] Jpeg(int width, int height)
    {
        using var noise = Image.Gaussnoise(width, height, mean: Random.Shared.Next(60, 200), sigma: 30);
        using var band = noise.Cast(Enums.BandFormat.Uchar);
        using var rgb = band.Bandjoin(band, band);
        using var srgb = rgb.Copy(interpretation: Enums.Interpretation.Srgb);
        return srgb.JpegsaveBuffer(q: 60);
    }

    private Task<HttpResponseMessage> UploadAsync(string token, byte[] content, string altText = "Món ăn")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "anh.jpg");
        form.Add(new StringContent(altText), "altText");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{Admin}/images") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _http.SendAsync(request);
    }

    private async Task<JsonElement> PublicAsync(string path)
    {
        var response = await _http.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private static async Task<string?> ErrorOf(Task<HttpResponseMessage> request) =>
        await AuthApiClient.ReadErrorCodeAsync(await request);
}
