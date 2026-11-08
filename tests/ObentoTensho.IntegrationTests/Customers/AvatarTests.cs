using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetVips;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Customers.Services;
using ObentoTensho.Core.Maintenance.Services;
using ObentoTensho.Core.Media.Domain;
using ObentoTensho.Core.Media.Services;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Customers;

// POST/DELETE /account/avatar (docs/srs/10 §5): ảnh đại diện đi qua pipeline ảnh, chỉ tài khoản đã xác thực, có giới hạn mỗi ngày
[Collection(SqlServerFixture.CollectionName)]
public sealed class AvatarTests : IDisposable
{
    private const string Account = "/api/v1/account";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AuthApiClient _auth;

    public AvatarTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _auth = new AuthApiClient(_http);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    private static byte[] Jpeg(int size = 96)
    {
        using var noise = Image.Gaussnoise(size, size, mean: Random.Shared.Next(60, 200), sigma: 30);
        using var band = noise.Cast(Enums.BandFormat.Uchar);
        using var rgb = band.Bandjoin(band, band);
        using var srgb = rgb.Copy(interpretation: Enums.Interpretation.Srgb);
        return srgb.JpegsaveBuffer(q: 70);
    }

    private async Task<(Guid Id, string Access)> CustomerAsync(bool verified = true)
    {
        var email = AuthApiClient.NewEmail();
        var (access, _) = await _auth.RegisterAndLoginAsync(email);
        await using var db = _fixture.CreateContext();
        var id = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsEmailVerified, verified));
        return (id, access);
    }

    private async Task<HttpResponseMessage> UploadAsync(string access, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "anh.jpg");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Account}/avatar") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return await _http.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string access)
    {
        using var request = new HttpRequestMessage(method, $"{Account}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return await _http.SendAsync(request);
    }

    private async Task<JsonElement> ProfileAsync(string access) =>
        await AdminAuthApiClient.ReadDataAsync(await SendAsync(HttpMethod.Get, "/profile", access));

    [Fact]
    public async Task Tai_doi_va_xoa_anh_dai_dien()
    {
        var (_, unverified) = await CustomerAsync(verified: false);
        Assert.Equal(ErrorCodes.EmailNotVerified, await AuthApiClient.ReadErrorCodeAsync(await UploadAsync(unverified, Jpeg())));

        var (id, access) = await CustomerAsync();
        Assert.Equal(JsonValueKind.Null, (await ProfileAsync(access)).GetProperty("avatar").ValueKind);
        Assert.Equal(ErrorCodes.FileInvalid, await AuthApiClient.ReadErrorCodeAsync(await UploadAsync(access, "khong phai anh"u8.ToArray())));
        Assert.Equal(ErrorCodes.FileTooLarge, await AuthApiClient.ReadErrorCodeAsync(await UploadAsync(access, new byte[MediaRules.AvatarMaxBytes + 1])));

        var first = await AdminAuthApiClient.ReadDataAsync(await UploadAsync(access, Jpeg()));
        var firstId = first.GetProperty("id").GetGuid();
        Assert.Equal("ready", first.GetProperty("status").GetString());
        Assert.Equal(firstId, (await ProfileAsync(access)).GetProperty("avatar").GetProperty("id").GetGuid());

        var secondId = (await AdminAuthApiClient.ReadDataAsync(await UploadAsync(access, Jpeg()))).GetProperty("id").GetGuid();
        await using (var db = _fixture.CreateContext())
        {
            Assert.Equal(secondId, await db.Users.Where(u => u.Id == id).Select(u => u.AvatarImageId).SingleAsync());
            var image = await db.Images.SingleAsync(i => i.Id == secondId);
            Assert.Equal((id, (Guid?)null), (image.CreatedByUserId!.Value, image.CreatedByStaffId));
            Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "customers.avatar.changed" && a.EntityId == id.ToString()));

            // Ảnh cũ không còn dùng và ảnh đang dùng đều đã quá 30 ngày: chỉ ảnh cũ bị dọn
            await db.Images.Where(i => i.Id == firstId || i.Id == secondId).ExecuteUpdateAsync(s => s.SetProperty(i => i.CreatedAtUtc, DateTime.UtcNow.AddDays(-31)));
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetServices<IRecurringJob>().OfType<ImageOrphanCleanupJob>().Single().CleanAsync(CancellationToken.None);
        }

        await using (var db = _fixture.CreateContext())
        {
            Assert.False(await db.Images.AnyAsync(i => i.Id == firstId));
            Assert.True(await db.Images.AnyAsync(i => i.Id == secondId));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, "/avatar", access)).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ProfileAsync(access)).GetProperty("avatar").ValueKind);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, "/avatar", access)).StatusCode);
        await using (var db = _fixture.CreateContext())
        {
            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "customers.avatar.removed" && a.EntityId == id.ToString()));
            Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "customers.avatar.removed" && a.EntityId == id.ToString()));
        }
    }

    [Fact]
    public async Task Gioi_han_so_lan_doi_moi_ngay()
    {
        var (_, access) = await CustomerAsync();
        var image = Jpeg(32);
        for (var i = 0; i < MediaRules.AvatarUploadsPerDay; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await UploadAsync(access, image)).StatusCode);
        }

        Assert.Equal(ErrorCodes.RateLimited, await AuthApiClient.ReadErrorCodeAsync(await UploadAsync(access, image)));
    }

    [Fact]
    public async Task Xoa_tai_khoan_bo_anh_dai_dien_va_don_du_lieu_khong_xoa_tai_khoan_co_anh()
    {
        var (id, access) = await CustomerAsync();
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(access, Jpeg())).StatusCode);
        await using (var db = _fixture.CreateContext())
        {
            await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.DeletionScheduledAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AccountDeletionService>().AnonymizeDueAsync(CancellationToken.None);
        }

        await using (var db = _fixture.CreateContext())
        {
            Assert.Null(await db.Users.Where(u => u.Id == id).Select(u => u.AvatarImageId).SingleAsync());
        }

        // Tài khoản chưa xác thực quá hạn nhưng từng tải ảnh: giữ lại thay vì vỡ khóa ngoại của ảnh
        var (other, otherAccess) = await CustomerAsync();
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(otherAccess, Jpeg())).StatusCode);
        await using (var db = _fixture.CreateContext())
        {
            await db.Users.Where(u => u.Id == other).ExecuteUpdateAsync(s => s
                .SetProperty(u => u.IsEmailVerified, false)
                .SetProperty(u => u.AvatarImageId, (Guid?)null)
                .SetProperty(u => u.CreatedAtUtc, DateTime.UtcNow.AddDays(-31)));
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetServices<IRecurringJob>().OfType<DataRetentionJob>().Single().RunAsync(CancellationToken.None);
        }

        await using (var check = _fixture.CreateContext())
        {
            Assert.True(await check.Users.AnyAsync(u => u.Id == other));
        }
    }
}
