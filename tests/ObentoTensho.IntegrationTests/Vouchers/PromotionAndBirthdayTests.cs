using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Vouchers.Abstractions;
using ObentoTensho.Core.Vouchers.Services;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;

namespace ObentoTensho.IntegrationTests.Vouchers;

// docs/srs/14 §8 (khuyến mãi, xuất Excel, giới hạn kiểm mã) và BR-LOY-09/20 (voucher sinh nhật)
[Collection(SqlServerFixture.CollectionName)]
public sealed class PromotionAndBirthdayTests : IDisposable
{
    private const string AdminPromotions = "/api/v1/admin/promotions";
    private const string AdminVouchers = "/api/v1/admin/vouchers";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;

    public PromotionAndBirthdayTests(SqlServerFixture fixture)
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
    public async Task Trang_cong_khai_chi_hien_khuyen_mai_dang_chay_hoac_sap_dien_ra_co_cho_phep()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var tag = Guid.NewGuid().ToString("N")[..8];

        var running = await CreateAsync(manager, $"chay-{tag}", Today.AddDays(-1), Today.AddDays(1));
        var oneDay = await CreateAsync(manager, $"mot-ngay-{tag}", Today, Today);
        var teaser = await CreateAsync(manager, $"teaser-{tag}", Today.AddDays(3), Today.AddDays(5), publicTeaser: true);
        var hiddenSoon = await CreateAsync(manager, $"sap-an-{tag}", Today.AddDays(3), Today.AddDays(5));
        var ended = await CreateAsync(manager, $"het-{tag}", Today.AddDays(-5), Today.AddDays(-1));
        var draft = await CreateAsync(manager, $"nhap-{tag}", Today.AddDays(-1), Today.AddDays(1), isActive: false);

        var list = (await PublicAsync("/api/v1/promotions")).EnumerateArray().ToDictionary(p => p.GetProperty("slug").GetString()!, StringComparer.Ordinal);
        Assert.True(list.ContainsKey(Slug(running)) && list.ContainsKey(Slug(oneDay)));
        Assert.True(list[Slug(teaser)].GetProperty("upcoming").GetBoolean());
        Assert.False(list.ContainsKey(Slug(hiddenSoon)) || list.ContainsKey(Slug(ended)) || list.ContainsKey(Slug(draft)));

        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/v1/promotions/{Slug(draft)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/v1/promotions/{Slug(ended)}")).StatusCode);

        // Mô tả được làm sạch; trùng slug ⇒ 409; ngày kết thúc trước ngày bắt đầu ⇒ 400
        var detail = await PublicAsync($"/api/v1/promotions/{Slug(running)}");
        Assert.Equal("<p>Giảm giá</p>", detail.GetProperty("descriptionHtml").GetString());
        Assert.Equal(ErrorCodes.DuplicateName, await ErrorOf(_admin.SendAsync(HttpMethod.Post, AdminPromotions, manager, Body($"chay-{tag}", Today, Today))));
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, AdminPromotions, manager, Body($"sai-{tag}", Today, Today.AddDays(-1)))).StatusCode);

        // Xóa mềm: biến mất ngay (cache đã xóa), slug vẫn giữ chỗ
        var etag = (await _admin.SendAsync(HttpMethod.Get, $"{AdminPromotions}/{running.GetProperty("id").GetInt32()}", manager)).Headers.ETag?.Tag;
        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Delete, $"{AdminPromotions}/{running.GetProperty("id").GetInt32()}", manager, ifMatch: etag)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/v1/promotions/{Slug(running)}")).StatusCode);
        Assert.Equal(ErrorCodes.DuplicateName, await ErrorOf(_admin.SendAsync(HttpMethod.Post, AdminPromotions, manager, Body($"chay-{tag}", Today, Today))));

        var reception = await _admin.SignInAsync(StaffRole.Reception);
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Post, AdminPromotions, reception, Body($"le-tan-{tag}", Today, Today))).StatusCode);
    }

    [Fact]
    public async Task Khuyen_mai_chi_hien_ma_dung_chung_con_dung_duoc_va_xuat_Excel_co_audit()
    {
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var promotion = await CreateAsync(manager, $"ma-{tag}", Today, Today.AddDays(2));
        var id = promotion.GetProperty("id").GetInt32();

        var shared = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, AdminVouchers, manager, Voucher(id)));
        var code = shared.GetProperty("code").GetString()!;
        var bulk = await AdminAuthApiClient.ReadDataAsync(await _admin.SendAsync(HttpMethod.Post, $"{AdminVouchers}/bulk-generate", manager, new { prefix = (string?)null, quantity = 2, template = Voucher(id) }));
        Assert.Equal(ErrorCodes.ValidationFailed, await ErrorOf(_admin.SendAsync(HttpMethod.Post, AdminVouchers, manager, Voucher(-1))));

        var codes = (await PublicAsync($"/api/v1/promotions/{Slug(promotion)}")).GetProperty("voucherCodes").EnumerateArray().Select(c => c.GetString()).ToArray();
        Assert.Equal([code], codes, StringComparer.Ordinal);

        var export = await _admin.SendAsync(HttpMethod.Get, $"{AdminVouchers}/export?batchId={bulk.GetProperty("batchId").GetGuid()}", manager);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        using var book = new XLWorkbook(new MemoryStream(await export.Content.ReadAsByteArrayAsync()));
        var sheet = book.Worksheet(1);
        Assert.Equal("Mã", sheet.Cell(1, 1).GetString());
        Assert.Equal(3, sheet.LastRowUsed()!.RowNumber());
        Assert.Equal(XLDataType.Text, sheet.Cell(2, 1).DataType);

        await using var db = _fixture.CreateContext();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "vouchers.voucher.exported"));
    }

    [Fact]
    public async Task Voucher_sinh_nhat_moi_nam_mot_lan_chi_cho_khach_da_xac_thuc_email()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var (verified, email) = await MemberAsync(emailVerified: true, Today.AddYears(-30));
        var (unverified, _) = await MemberAsync(emailVerified: false, Today.AddYears(-30));
        var (otherDay, _) = await MemberAsync(emailVerified: true, Today.AddDays(40).AddYears(-30));
        var (disposable, _) = await MemberAsync(emailVerified: true, Today.AddYears(-30), $"khach.{Guid.NewGuid():N}@mailinator.com");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var birthdays = scope.ServiceProvider.GetRequiredService<BirthdayVoucherService>();
            Assert.True(await birthdays.IssueDueAsync(CancellationToken.None) >= 1);
            Assert.Equal(0, await birthdays.IssueDueAsync(CancellationToken.None));
        }

        await using var db = _fixture.CreateContext();
        var voucher = await db.Vouchers.SingleAsync(v => v.OwnerUserId == verified);
        Assert.Equal((VoucherSource.Birthday, Today.Year, 50_000m, 1), (voucher.Source, voucher.BirthdayYear!.Value, voucher.DiscountValue, voucher.MaxUsageTotal!.Value));
        Assert.False(await db.Vouchers.AnyAsync(v => v.OwnerUserId == unverified || v.OwnerUserId == otherDay));

        // BR-AUTH-16: email dùng một lần không nhận ưu đãi
        Assert.False(await db.Vouchers.AnyAsync(v => v.OwnerUserId == disposable));

        await _factory.ProcessOutboxAsync();
        Assert.Contains(_factory.Emails.To(email), m => m.TextBody.Contains(voucher.Code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sinh_29_thang_2_nhan_vao_28_thang_2_nam_thuong()
    {
        var (leap, _) = await MemberAsync(emailVerified: true, new DateOnly(2004, 2, 29));

        await using var scope = _factory.Services.CreateAsyncScope();
        var vouchers = scope.ServiceProvider.GetRequiredService<IVoucherRepository>();

        // Năm chưa cấp voucher nào nên chỉ phụ thuộc điều kiện ngày
        Assert.Contains(await vouchers.ListBirthdayDueAsync(2, 28, includeLeapDay: true, 2099, afterId: null, 100_000, CancellationToken.None), u => u.Id == leap);
        Assert.DoesNotContain(await vouchers.ListBirthdayDueAsync(2, 28, includeLeapDay: false, 2099, afterId: null, 100_000, CancellationToken.None), u => u.Id == leap);
    }

    [Fact]
    public async Task Moi_tai_khoan_kiem_ma_toi_da_30_lan_moi_gio()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var email = AuthApiClient.NewEmail();
        var (token, _) = await new AuthApiClient(_http).RegisterAndLoginAsync(email);

        for (var i = 0; i < 30; i++)
        {
            Assert.Equal(ErrorCodes.VoucherInvalid, await ErrorOf(ValidateAsync(token, menu)));
        }

        Assert.Equal(ErrorCodes.RateLimited, await ErrorOf(ValidateAsync(token, menu)));
    }

    private Task<HttpResponseMessage> ValidateAsync(string token, OrderTestMenu menu)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/vouchers/validate")
        {
            Content = JsonContent.Create(new { code = "KHONG-CO-MA", items = new[] { OrderApiClient.Line(menu.Tea, 1) } }),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return _http.SendAsync(request);
    }

    private async Task<JsonElement> CreateAsync(string token, string slug, DateOnly start, DateOnly end, bool publicTeaser = false, bool isActive = true)
    {
        var response = await _admin.SendAsync(HttpMethod.Post, AdminPromotions, token, Body(slug, start, end, publicTeaser, isActive));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await AdminAuthApiClient.ReadDataAsync(response);
    }

    private static object Body(string slug, DateOnly start, DateOnly end, bool publicTeaser = false, bool isActive = true) => new
    {
        title = $"Khuyến mãi {slug}",
        slug,
        descriptionHtml = "<p onclick=\"x()\">Giảm giá</p><script>alert(1)</script>",
        startDate = OrderApiClient.Date(start),
        endDate = OrderApiClient.Date(end),
        publicTeaser,
        isActive,
    };

    private static object Voucher(int promotionId) => new
    {
        code = (string?)null,
        discountType = "fixed",
        discountValue = 10_000,
        maxDiscountAmount = (decimal?)null,
        minOrderValue = 0,
        maxUsageTotal = 10,
        maxUsagePerUser = 1,
        applicableTier = (string?)null,
        requireLogin = false,
        startDate = (string?)null,
        endDate = (string?)null,
        isActive = true,
        promotionId,
    };

    private static string Slug(JsonElement promotion) => promotion.GetProperty("slug").GetString()!;

    // Hơn một lô (500) tài khoản email dùng một lần đứng trước khách thật theo thứ tự Id: khách thật vẫn nhận voucher
    [Fact]
    public async Task Voucher_sinh_nhat_khong_bi_tai_khoan_email_tam_chiem_cho()
    {
        var (template, _) = await MemberAsync(emailVerified: true, Today.AddYears(-25), $"mau.{Guid.NewGuid():N}@mailinator.com");
        var (real, _) = await MemberAsync(emailVerified: true, Today.AddYears(-25));
        await using (var db = _fixture.CreateContext())
        {
            // Nhân bản tài khoản mẫu; nhóm cuối của Id nhỏ nhất nên SQL Server xếp chúng trước mọi Id thật
            var tag = Guid.NewGuid().ToString("N")[..8];
            await db.Database.ExecuteSqlRawAsync(
                """
                DECLARE @cols nvarchar(max) = (SELECT STRING_AGG(QUOTENAME(name), ',') FROM sys.columns
                    WHERE object_id = OBJECT_ID('Users') AND is_computed = 0 AND system_type_id <> 189
                    AND name NOT IN ('Id', 'Email', 'EmailCanonical') AND name NOT LIKE '%TokenHash');
                DECLARE @sql nvarchar(max) = N'INSERT INTO [Users] ([Id], [Email], [EmailCanonical], ' + @cols + N')
                    SELECT CONVERT(uniqueidentifier, @tag + ''-0000-0000-0000-'' + RIGHT(''00000000000'' + CAST(x.n AS varchar(12)), 12)),
                        CONCAT(''tam'', x.n, ''.'', @tag, ''@mailinator.com''), CONCAT(''tam'', x.n, ''.'', @tag, ''@mailinator.com''), ' + @cols + N'
                    FROM [Users] CROSS JOIN (SELECT TOP 510 ROW_NUMBER() OVER (ORDER BY (SELECT 1)) AS n FROM sys.all_objects) AS x
                    WHERE [Id] = @src';
                EXEC sp_executesql @sql, N'@tag varchar(8), @src uniqueidentifier', @tag = {0}, @src = {1};
                """,
                tag,
                template);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<BirthdayVoucherService>().IssueDueAsync(CancellationToken.None);
        }

        await using var check = _fixture.CreateContext();
        Assert.True(await check.Vouchers.AnyAsync(v => v.OwnerUserId == real && v.Source == VoucherSource.Birthday));
        Assert.False(await check.Vouchers.AnyAsync(v => v.OwnerUserId == template));
    }

    private async Task<(Guid Id, string Email)> MemberAsync(bool emailVerified, DateOnly dateOfBirth, string? address = null)
    {
        var email = address ?? AuthApiClient.NewEmail();
        await new AuthApiClient(_http).RegisterAndLoginAsync(email);
        await using var db = _fixture.CreateContext();
        await db.Users.Where(u => u.Email == email).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.IsEmailVerified, emailVerified)
            .SetProperty(u => u.DateOfBirth, dateOfBirth));
        return (await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync(), email);
    }

    private async Task<JsonElement> PublicAsync(string path)
    {
        var response = await _http.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await AdminAuthApiClient.ReadDataAsync(response);
    }

    private static async Task<string?> ErrorOf(Task<HttpResponseMessage> request) =>
        await AuthApiClient.ReadErrorCodeAsync(await request);
}
