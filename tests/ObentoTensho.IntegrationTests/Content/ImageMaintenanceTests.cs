using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetVips;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Media.Services;
using ObentoTensho.Core.Settings.Domain;
using ObentoTensho.Infrastructure.Media;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;

namespace ObentoTensho.IntegrationTests.Content;

// FR-IMG-08 (thử lại ảnh lỗi), BR-CNT-32/FR-IMG-10 (dọn ảnh không còn dùng)
[Collection(SqlServerFixture.CollectionName)]
public sealed class ImageMaintenanceTests : IDisposable
{
    private const string Admin = "/api/v1/admin";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public ImageMaintenanceTests(SqlServerFixture fixture)
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

    private static byte[] Jpeg()
    {
        using var noise = Image.Gaussnoise(300, 200, mean: Random.Shared.Next(60, 200), sigma: 30);
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
        form.Add(file, "file", "anh.jpg");
        form.Add(new StringContent("Ảnh"), "altText");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Admin}/images") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (await AdminAuthApiClient.ReadDataAsync(await _http.SendAsync(request))).GetProperty("id").GetGuid();
    }

    private async Task<int> RunAsync<T>()
        where T : IRecurringJob
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetServices<IRecurringJob>().OfType<T>().Single().RunAsync(CancellationToken.None);
    }

    private async Task SetImageAsync(Guid id, ImageStatus status, int attempts, DateTime? failedAt, DateTime? createdAt = null)
    {
        await using var db = _fixture.CreateContext();
        await db.Images.Where(i => i.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(i => i.Status, status)
            .SetProperty(i => i.ProcessingAttempts, attempts)
            .SetProperty(i => i.FailedAtUtc, failedAt));
        if (createdAt is { } at)
        {
            await db.Images.Where(i => i.Id == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.CreatedAtUtc, at));
        }
    }

    private async Task<ImageStatus> StatusAsync(Guid id)
    {
        await using var db = _fixture.CreateContext();
        return await db.Images.Where(i => i.Id == id).Select(i => i.Status).SingleAsync();
    }

    [Fact]
    public async Task Anh_loi_tu_thu_lai_toi_da_3_lan_cach_5_phut_va_thu_lai_bang_tay()
    {
        var editor = await _admin.SignInAsync(StaffRole.ContentEditor);
        var due = await UploadAsync(editor);
        var tooSoon = await UploadAsync(editor);
        var exhausted = await UploadAsync(editor);
        var now = DateTime.UtcNow;
        await SetImageAsync(due, ImageStatus.Failed, 1, now.AddMinutes(-6));
        await SetImageAsync(tooSoon, ImageStatus.Failed, 1, now.AddMinutes(-1));
        await SetImageAsync(exhausted, ImageStatus.Failed, 3, now.AddMinutes(-30));

        Assert.True(await RunAsync<ImageRetryJob>() >= 1);

        Assert.Equal(ImageStatus.Processing, await StatusAsync(due));
        Assert.Equal(ImageStatus.Failed, await StatusAsync(tooSoon));
        Assert.Equal(ImageStatus.Failed, await StatusAsync(exhausted));

        // Hết lượt: người quản trị bấm thử lại
        var retried = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{Admin}/images/{exhausted}/retry", editor));
        Assert.Equal("processing", retried.GetProperty("status").GetString());
        await using (var db = _fixture.CreateContext())
        {
            Assert.Equal(0, await db.Images.Where(i => i.Id == exhausted).Select(i => i.ProcessingAttempts).SingleAsync());
        }

        var ready = await UploadAsync(editor);
        Assert.Equal(HttpStatusCode.Conflict, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/images/{ready}/retry", editor)).StatusCode);
    }

    // FR-IMG-03/04: SuperAdmin yêu cầu sinh lại; ảnh vẫn dùng bộ cũ tới khi bộ mới (phiên bản kế tiếp) xong, tệp cũ được giữ
    [Fact]
    public async Task Sinh_lai_bien_the_len_phien_ban_moi_va_giu_ban_cu()
    {
        var superAdmin = await _admin.SignInAsync(StaffRole.SuperAdmin);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var image = await UploadAsync(manager);
        var broken = await UploadAsync(manager);
        var options = _factory.Services.GetRequiredService<IOptions<MediaOptions>>().Value;
        string VariantDir(Guid id, int version) => Path.Combine(options.PublicDirectory, "images", id.ToString("N"), version.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/images/regenerate", manager)).StatusCode);
        var queued = await _admin.SendAsync(HttpMethod.Post, $"{Admin}/images/regenerate", superAdmin);
        Assert.Equal(HttpStatusCode.Accepted, queued.StatusCode);
        Assert.True((await AdminAuthApiClient.ReadDataAsync(queued)).GetProperty("queued").GetInt32() >= 2);

        // Đang chờ: vẫn sẵn sàng, đường dẫn vẫn là phiên bản 1
        var waiting = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/images/{image}", manager));
        Assert.Equal("ready", waiting.GetProperty("status").GetString());
        Assert.Contains("/1/", waiting.GetProperty("variants")[0].GetProperty("url").GetString(), StringComparison.Ordinal);

        // Ảnh gốc mất ⇒ sinh lại lỗi nhưng ảnh vẫn dùng bộ cũ
        await using (var db = _fixture.CreateContext())
        {
            var original = await db.Images.Where(i => i.Id == broken).Select(i => i.OriginalPath).SingleAsync();
            File.Delete(Path.Combine(options.OriginalsDirectory, Path.GetFileName(original)));
        }

        // Có trần số vòng: lỗi làm cờ không bao giờ được xóa thì test đỏ chứ không treo
        for (var round = 0; round < 500 && await RunAsync<ImageProcessingJob>() > 0; round++)
        {
        }

        var done = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Get, $"{Admin}/images/{image}", manager));
        Assert.Equal("ready", done.GetProperty("status").GetString());
        Assert.Contains("/2/", done.GetProperty("variants")[0].GetProperty("url").GetString(), StringComparison.Ordinal);
        Assert.True(Directory.Exists(VariantDir(image, 2)));
        Assert.True(Directory.Exists(VariantDir(image, 1)));
        await using (var db = _fixture.CreateContext())
        {
            var failed = await db.Images.SingleAsync(i => i.Id == broken);
            Assert.Equal((1, false, ImageStatus.Ready), (failed.Version, failed.RegenerateRequested, failed.Status));
            Assert.False(await db.Images.AnyAsync(i => i.Id == image && i.RegenerateRequested));
            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "media.image.regenerate_requested"));
            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "media.image.regenerate_failed" && a.EntityId == broken.ToString()));
        }
    }

    [Fact]
    public async Task Don_anh_qua_30_ngay_khong_con_dung_va_giu_anh_dang_dung()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var tag = Guid.NewGuid().ToString("N")[..10];
        var orphan = await UploadAsync(manager);
        var inPromotion = await UploadAsync(manager);
        var inHtml = await UploadAsync(manager);
        var inSettings = await UploadAsync(manager);
        var recent = await UploadAsync(manager);

        Assert.Equal(HttpStatusCode.Created, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/promotions", manager, new
        {
            title = $"KM {tag}", slug = $"km-anh-{tag}", descriptionHtml = (string?)null,
            startDate = OrderApiClient.Date(TestSchedule.TodayVn), endDate = OrderApiClient.Date(TestSchedule.TodayVn.AddDays(3)),
            publicTeaser = false, isActive = true, bannerImageId = inPromotion,
        })).StatusCode);
        var html = $"<p>Ảnh</p><img src=\"/uploads/images/{inHtml:N}/1/480.webp\" alt=\"a\">";
        Assert.Equal(HttpStatusCode.Created, (await _admin.SendAsync(HttpMethod.Post, $"{Admin}/news", manager, new
        {
            title = $"Tin {tag}", slug = (string?)null, thumbnailImageId = (Guid?)null, summary = (string?)null, contentHtml = html,
            categoryId = (int?)null, authorDisplayName = (string?)null, metaTitle = (string?)null, metaDescription = (string?)null, ogImageId = (Guid?)null,
            status = "draft", publishedAtUtc = (DateTime?)null,
        })).StatusCode);

        string originalOf;
        await using (var db = _fixture.CreateContext())
        {
            var old = DateTime.UtcNow.AddDays(-31);
            await db.Images.Where(i => i.Id == orphan || i.Id == inPromotion || i.Id == inHtml || i.Id == inSettings)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.CreatedAtUtc, old));
            await db.SystemSettings.Where(s => s.Key == SettingKeys.RestaurantLogo).ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, inSettings.ToString()));
            originalOf = await db.Images.Where(i => i.Id == orphan).Select(i => i.OriginalPath).SingleAsync();
        }

        var options = _factory.Services.GetRequiredService<IOptions<MediaOptions>>().Value;
        var originalFile = Path.Combine(options.OriginalsDirectory, Path.GetFileName(originalOf));
        Assert.True(File.Exists(originalFile));

        try
        {
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var job = scope.ServiceProvider.GetServices<IRecurringJob>().OfType<ImageOrphanCleanupJob>().Single();
                Assert.True(await job.CleanAsync(CancellationToken.None) >= 1);
            }

            await using var check = _fixture.CreateContext();
            Assert.False(await check.Images.AnyAsync(i => i.Id == orphan));
            Assert.False(File.Exists(originalFile));
            Assert.False(Directory.Exists(Path.Combine(options.PublicDirectory, "images", orphan.ToString("N"))));
            foreach (var kept in new[] { inPromotion, inHtml, inSettings, recent })
            {
                Assert.True(await check.Images.AnyAsync(i => i.Id == kept));
            }
        }
        finally
        {
            await using var db = _fixture.CreateContext();
            await db.SystemSettings.Where(s => s.Key == SettingKeys.RestaurantLogo).ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, string.Empty));
        }
    }
}
