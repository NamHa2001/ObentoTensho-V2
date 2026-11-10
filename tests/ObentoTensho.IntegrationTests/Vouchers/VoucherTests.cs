using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Loyalty.Services;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;

namespace ObentoTensho.IntegrationTests.Vouchers;

// docs/srs/14 §8: kiểm mã ở giỏ, giữ lượt nguyên tử khi đặt, hoàn khi hủy, voucher cá nhân, đổi điểm, duyệt bốn mắt
[Collection(SqlServerFixture.CollectionName)]
public sealed class VoucherTests : IDisposable
{
    private const string Admin = "/api/v1/admin/vouchers";
    private const string Rewards = "/api/v1/admin/redeem-rewards";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AdminAuthApiClient _admin;
    private readonly OrderApiClient _orders;

    public VoucherTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _http = _factory.CreateApiClient();
        _admin = new AdminAuthApiClient(_factory, fixture);
        _orders = new OrderApiClient(_http);
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Ma_viet_thuong_hop_le_dat_don_duoc_giam_va_huy_thi_hoan_luot()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var code = await CreateVoucherAsync(manager, new { discountValue = 10, maxDiscountAmount = 20_000, minOrderValue = 50_000, maxUsageTotal = 10 });
        var items = new[] { OrderApiClient.Line(menu.Rice, 2, menu.Large) };

        // Tạm tính 160.000 ⇒ 10% = 16.000
        var check = await Data(Validate(code.ToLowerInvariant(), items));
        Assert.Equal((160_000m, 16_000m, 144_000m), (check.GetProperty("subTotal").GetDecimal(), check.GetProperty("voucherDiscount").GetDecimal(), check.GetProperty("totalWithVoucher").GetDecimal()));
        Assert.Equal(ErrorCodes.VoucherMinOrder, await ErrorOf(Validate(code, [OrderApiClient.Line(menu.Tea, 1)])));
        Assert.Equal(ErrorCodes.VoucherInvalid, await ErrorOf(Validate("KHONG-CO-MA", items)));

        var created = await OrderApiClient.DataOf(await _orders.CreateAsync(Order(items, code)));
        var order = created.GetProperty("order");
        Assert.Equal((16_000m, 0m, 144_000m), (order.GetProperty("voucherDiscount").GetDecimal(), order.GetProperty("memberDiscount").GetDecimal(), order.GetProperty("totalAmount").GetDecimal()));
        Assert.Equal(1, await UsedCountAsync(code));

        var cancel = await _orders.CancelAsync(order.GetProperty("code").GetString()!, created.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(0, await UsedCountAsync(code));
    }

    [Fact]
    public async Task Ba_muoi_don_dong_thoi_voi_voucher_toi_da_5_luot_thi_dung_5_don_duoc_giam()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var code = await CreateVoucherAsync(manager, new { discountType = "fixed", discountValue = 10_000, maxUsageTotal = 5 });

        var responses = await Task.WhenAll(Enumerable.Range(0, 30)
            .Select(_ => _orders.CreateAsync(Order([OrderApiClient.Line(menu.Tea, 3)], code))));

        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(5, await UsedCountAsync(code));
        await using var db = _fixture.CreateContext();
        Assert.Equal(5, await db.Orders.CountAsync(o => o.VoucherCodeSnapshot == code));
    }

    [Fact]
    public async Task Doi_diem_lay_voucher_rieng_chi_chu_dung_duoc_va_doi_dong_thoi_chi_ra_mot_ma()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var (owner, ownerToken) = await MemberAsync();
        var (_, otherToken) = await MemberAsync();
        await GivePointsAsync(owner, 150);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var reward = await RewardAsync(manager, new { name = $"Giảm 30k {Guid.NewGuid():N}", pointsCost = 100, discountType = "fixed", discountValue = 30_000, minOrderValue = 0 });

        // Khách thấy phần thưởng trước khi đổi; phần thưởng không tồn tại thì không đổi được
        var listed = (await Data(Send(HttpMethod.Get, "/api/v1/account/rewards", ownerToken))).EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == reward);
        Assert.Equal(100, listed.GetProperty("pointsCost").GetInt32());
        Assert.Equal(ErrorCodes.NotFound, await ErrorOf(Send(HttpMethod.Post, "/api/v1/account/redeem-points", ownerToken, new { rewardId = int.MaxValue })));

        var redeems = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Send(HttpMethod.Post, "/api/v1/account/redeem-points", ownerToken, new { rewardId = reward })));
        Assert.Equal(1, redeems.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(redeems.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        var mine = (await Data(Send(HttpMethod.Get, "/api/v1/account/vouchers", ownerToken))).EnumerateArray().Single();
        var code = mine.GetProperty("code").GetString()!;
        Assert.StartsWith("DIEM-", code, StringComparison.Ordinal);
        Assert.Equal(30_000m, mine.GetProperty("discountValue").GetDecimal());

        await using (var db = _fixture.CreateContext())
        {
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == owner);
            Assert.Equal(50, user.CurrentPoints);
            Assert.Equal(1, await db.LoyaltyPoints.CountAsync(e => e.UserId == owner && e.Type == LoyaltyTransactionType.Redeem));
        }

        var items = new[] { OrderApiClient.Line(menu.Rice, 1, menu.Small) };
        Assert.Equal(ErrorCodes.VoucherInvalid, await ErrorOf(Validate(code, items, otherToken)));
        Assert.Equal(ErrorCodes.VoucherInvalid, await ErrorOf(Validate(code, items)));
        Assert.Equal(HttpStatusCode.OK, (await Validate(code, items, ownerToken)).StatusCode);

        // Một lần dùng: đơn thứ hai với cùng mã bị từ chối
        Assert.Equal(HttpStatusCode.Created, (await _orders.CreateAsync(Order(items, code), ownerToken)).StatusCode);
        Assert.Equal(ErrorCodes.VoucherExhausted, await ErrorOf(_orders.CreateAsync(Order(items, code), ownerToken)));

        await _factory.ProcessOutboxAsync();
        var owners = await _fixture.CreateContext().Users.AsNoTracking().Where(u => u.Id == owner).Select(u => u.Email).SingleAsync();
        Assert.Contains(_factory.Emails.To(owners), m => m.TextBody.Contains(code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Phan_thuong_doi_diem_quan_tri_dat_va_ma_doi_duoc_chep_dung_dieu_kien()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var (owner, token) = await MemberAsync();
        await GivePointsAsync(owner, 500);
        var name = $"Giảm 20% {Guid.NewGuid():N}";
        var id = await RewardAsync(manager, new { name, pointsCost = 200, discountType = "percent", discountValue = 20, maxDiscountAmount = 50_000, minOrderValue = 100_000, validDays = 7 });

        Assert.Equal(ErrorCodes.DuplicateName, await ErrorOf(_admin.SendAsync(HttpMethod.Post, Rewards, manager, Reward(new { name, pointsCost = 1 }))));
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, Rewards, manager, Reward(new { name = "<b>x</b>", pointsCost = 1 }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.SendAsync(HttpMethod.Post, Rewards, manager, Reward(new { name = "Sai phần trăm", discountType = "percent", discountValue = 150 }))).StatusCode);

        var voucher = await Data(Send(HttpMethod.Post, "/api/v1/account/redeem-points", token, new { rewardId = id }));
        Assert.Equal(("percent", 20m, 50_000m, 100_000m), (voucher.GetProperty("discountType").GetString(), voucher.GetProperty("discountValue").GetDecimal(), voucher.GetProperty("maxDiscountAmount").GetDecimal(), voucher.GetProperty("minOrderValue").GetDecimal()));
        Assert.Equal(TestSchedule.TodayVn.AddDays(7), DateOnly.Parse(voucher.GetProperty("endDate").GetString()!, System.Globalization.CultureInfo.InvariantCulture));

        // Tắt phần thưởng: khách không thấy, không đổi được nữa; sửa cần If-Match
        var row = (await Data(_admin.SendAsync(HttpMethod.Get, Rewards, manager))).EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == id);
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await _admin.SendAsync(HttpMethod.Put, $"{Rewards}/{id}", manager, Reward(new { name, isActive = false }))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _admin.SendAsync(HttpMethod.Put, $"{Rewards}/{id}", manager, Reward(new { name, isActive = false }), ifMatch: $"\"{row.GetProperty("version").GetString()}\"")).StatusCode);
        Assert.DoesNotContain((await Data(Send(HttpMethod.Get, "/api/v1/account/rewards", token))).EnumerateArray(), r => r.GetProperty("id").GetInt32() == id);
        Assert.Equal(ErrorCodes.NotFound, await ErrorOf(Send(HttpMethod.Post, "/api/v1/account/redeem-points", token, new { rewardId = id })));

        var reception = await _admin.SignInAsync(StaffRole.Reception);
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, Rewards, reception)).StatusCode);
    }

    [Fact]
    public async Task Hop_chon_ma_liet_ke_ma_rieng_va_ma_khuyen_mai_kem_ly_do_chua_dung_duoc()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var (owner, token) = await MemberAsync();
        var (_, otherToken) = await MemberAsync();
        await GivePointsAsync(owner, 100);
        var reward = await RewardAsync(manager, new { name = $"Giảm 5k {Guid.NewGuid():N}", pointsCost = 100, discountType = "fixed", discountValue = 5_000 });
        var mine = (await Data(Send(HttpMethod.Post, "/api/v1/account/redeem-points", token, new { rewardId = reward }))).GetProperty("code").GetString()!;

        var tag = Guid.NewGuid().ToString("N")[..8];
        var promotion = await Data(_admin.SendAsync(HttpMethod.Post, "/api/v1/admin/promotions", manager, new { title = $"Khai trương {tag}", slug = $"khai-truong-{tag}", descriptionHtml = (string?)null, startDate = TestSchedule.TodayVn, endDate = TestSchedule.TodayVn.AddDays(3), publicTeaser = true, isActive = true, bannerImageId = (Guid?)null }));
        var promoted = await CreateVoucherAsync(manager, new { discountType = "fixed", discountValue = 1_000, minOrderValue = 10_000_000, promotionId = promotion.GetProperty("id").GetInt32() });
        var hidden = await CreateVoucherAsync(manager, new { discountType = "fixed", discountValue = 2_000 });

        var items = new[] { OrderApiClient.Line(menu.Tea, 2) };
        var options = (await Data(Send(HttpMethod.Post, "/api/v1/account/voucher-options", token, new { items }))).EnumerateArray().ToArray();
        var codes = options.Select(o => o.GetProperty("code").GetString()).ToArray();
        Assert.Contains(mine, codes, StringComparer.Ordinal);
        Assert.DoesNotContain(hidden, codes, StringComparer.Ordinal);
        var own = options.Single(o => string.Equals(o.GetProperty("code").GetString(), mine, StringComparison.Ordinal));
        Assert.True(own.GetProperty("usable").GetBoolean());
        Assert.Equal(5_000m, own.GetProperty("discount").GetDecimal());
        var promo = options.Single(o => string.Equals(o.GetProperty("code").GetString(), promoted, StringComparison.Ordinal));
        Assert.Equal((false, ErrorCodes.VoucherMinOrder, $"Khai trương {tag}"), (promo.GetProperty("usable").GetBoolean(), promo.GetProperty("errorCode").GetString(), promo.GetProperty("title").GetString()));

        // Mã riêng của người khác không lọt vào danh sách; chưa đăng nhập thì không gọi được
        Assert.DoesNotContain((await Data(Send(HttpMethod.Post, "/api/v1/account/voucher-options", otherToken, new { items }))).EnumerateArray(), o => string.Equals(o.GetProperty("code").GetString(), mine, StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Unauthorized, (await _http.PostAsJsonAsync("/api/v1/account/voucher-options", new { items })).StatusCode);
    }

    [Fact]
    public async Task Voucher_can_dang_nhap_va_gioi_han_moi_khach()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var manager = await _admin.SignInAsync(StaffRole.Manager);
        var code = await CreateVoucherAsync(manager, new { discountType = "fixed", discountValue = 5_000, maxUsageTotal = 100, requireLogin = true, maxUsagePerUser = 1 });
        var (_, token) = await MemberAsync();
        var items = new[] { OrderApiClient.Line(menu.Tea, 2) };

        Assert.Equal(ErrorCodes.VoucherLoginRequired, await ErrorOf(Validate(code, items)));
        Assert.Equal(HttpStatusCode.Created, (await _orders.CreateAsync(Order(items, code), token)).StatusCode);
        Assert.Equal(ErrorCodes.VoucherExhausted, await ErrorOf(Validate(code, items, token)));

        // userId trong thân request bị bỏ qua: khách lấy từ phiên (BR-VCH-09)
        var spoof = new HttpRequestMessage(HttpMethod.Post, "/api/v1/vouchers/validate") { Content = JsonContent.Create(new { code, items, userId = Guid.NewGuid() }) };
        Assert.Equal(HttpStatusCode.BadRequest, (await _http.SendAsync(spoof)).StatusCode);
    }

    [Fact]
    public async Task Voucher_gia_tri_cao_va_sinh_hang_loat_lon_can_nguoi_khac_duyet()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var creator = await _admin.SignInAsync(StaffRole.Manager);
        var reviewer = await _admin.SignInAsync(StaffRole.SuperAdmin);
        var items = new[] { OrderApiClient.Line(menu.Rice, 1, menu.Small) };

        var created = await Data(_admin.SendAsync(HttpMethod.Post, Admin, creator, Body(new { discountValue = 60, maxUsageTotal = 10 })));
        var id = created.GetProperty("id").GetInt32();
        var code = created.GetProperty("code").GetString()!;
        Assert.Equal("pending", created.GetProperty("approvalStatus").GetString());
        Assert.Equal(ErrorCodes.VoucherInvalid, await ErrorOf(Validate(code, items)));

        Assert.Equal(ErrorCodes.FourEyesRequired, await ErrorOf(_admin.SendAsync(HttpMethod.Post, $"{Admin}/{id}/approve", creator, new { note = (string?)null })));
        Assert.Equal("approved", (await Data(_admin.SendAsync(HttpMethod.Post, $"{Admin}/{id}/approve", reviewer, new { note = "OK khai trương" }))).GetProperty("approvalStatus").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Validate(code, items)).StatusCode);

        // 600 mã > 500 ⇒ cả lô chờ duyệt; mã ≥ 10 ký tự ngẫu nhiên, không trùng
        var bulk = await Data(_admin.SendAsync(HttpMethod.Post, $"{Admin}/bulk-generate", creator, new { prefix = "tet", quantity = 600, template = Body(new { discountType = "fixed", discountValue = 10_000, maxUsageTotal = 1 }) }));
        var codes = bulk.GetProperty("codes").EnumerateArray().Select(c => c.GetString()!).ToArray();
        Assert.Equal((600, "pending"), (codes.Distinct(StringComparer.Ordinal).Count(), bulk.GetProperty("approvalStatus").GetString()));
        Assert.All(codes, c => Assert.Matches("^TET-[0-9A-Z]{10}$", c));
        var batch = bulk.GetProperty("batchId").GetGuid();
        Assert.Equal(600, (await Data(_admin.SendAsync(HttpMethod.Post, $"{Admin}/batches/{batch}/approve", reviewer, new { note = (string?)null }))).GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await Validate(codes[0], items)).StatusCode);

        // Đã dùng thì chỉ tắt được; chưa dùng thì xóa được; lễ tân không có quyền
        var unused = await Data(_admin.SendAsync(HttpMethod.Post, Admin, creator, Body(new { discountType = "fixed", discountValue = 1_000, maxUsageTotal = 1 })));
        var unusedVersion = $"\"{unused.GetProperty("version").GetString()}\"";
        Assert.Equal(HttpStatusCode.NoContent, (await _admin.SendAsync(HttpMethod.Delete, $"{Admin}/{unused.GetProperty("id").GetInt32()}", creator, ifMatch: unusedVersion)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _orders.CreateAsync(Order(items, code))).StatusCode);
        var used = await _admin.SendAsync(HttpMethod.Get, $"{Admin}/{id}", creator);
        Assert.Equal(ErrorCodes.VoucherInUse, await ErrorOf(_admin.SendAsync(HttpMethod.Delete, $"{Admin}/{id}", creator, ifMatch: used.Headers.ETag?.Tag)));
        var reception = await _admin.SignInAsync(StaffRole.Reception);
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.SendAsync(HttpMethod.Get, Admin, reception)).StatusCode);
    }

    private static Dictionary<string, object?> Order(object[] items, string? voucherCode)
    {
        var order = ToDictionary(OrderApiClient.Order(items));
        order["voucherCode"] = voucherCode;
        return order;
    }

    private static Dictionary<string, object?> ToDictionary(object value) =>
        JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(value))!;

    // Mặc định: giảm %, mọi khách, không ngày hạn, đang bật
    private static Dictionary<string, object?> Body(object overrides)
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = null,
            ["discountType"] = "percent",
            ["discountValue"] = 10,
            ["maxDiscountAmount"] = null,
            ["minOrderValue"] = 0,
            ["maxUsageTotal"] = 100,
            ["maxUsagePerUser"] = 1,
            ["applicableTier"] = null,
            ["requireLogin"] = false,
            ["startDate"] = null,
            ["endDate"] = null,
            ["isActive"] = true,
        };
        foreach (var (key, value) in ToDictionary(overrides))
        {
            body[key] = value;
        }

        return body;
    }

    private async Task<string> CreateVoucherAsync(string token, object overrides)
    {
        var data = await Data(_admin.SendAsync(HttpMethod.Post, Admin, token, Body(overrides)));
        Assert.Equal("approved", data.GetProperty("approvalStatus").GetString());
        return data.GetProperty("code").GetString()!;
    }

    private Task<HttpResponseMessage> Validate(string code, object[] items, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/vouchers/validate") { Content = JsonContent.Create(new { code, items }) };
        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return _http.SendAsync(request);
    }

    private static Dictionary<string, object?> Reward(object overrides)
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = "Phần thưởng",
            ["description"] = null,
            ["pointsCost"] = 100,
            ["discountType"] = "fixed",
            ["discountValue"] = 10_000,
            ["maxDiscountAmount"] = null,
            ["minOrderValue"] = 0,
            ["validDays"] = 30,
            ["isActive"] = true,
        };
        foreach (var (key, value) in ToDictionary(overrides))
        {
            body[key] = value;
        }

        return body;
    }

    private async Task<int> RewardAsync(string token, object overrides) =>
        (await Data(_admin.SendAsync(HttpMethod.Post, Rewards, token, Reward(overrides)))).GetProperty("id").GetInt32();

    private Task<HttpResponseMessage> Send(HttpMethod method, string path, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return _http.SendAsync(request);
    }

    private async Task<(Guid Id, string Token)> MemberAsync()
    {
        var email = AuthApiClient.NewEmail();
        var (token, _) = await new AuthApiClient(_http).RegisterAndLoginAsync(email);
        await using var db = _fixture.CreateContext();
        return (await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync(), token);
    }

    private async Task GivePointsAsync(Guid userId, int points)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<LoyaltyLedger>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.InTransactionAsync(async ct =>
        {
            var earned = await ledger.EarnAsync(userId, LoyaltyTransactionType.OrderEarn, $"QUA-{userId:N}", points, null, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return earned;
        }, CancellationToken.None);
    }

    private async Task<int> UsedCountAsync(string code)
    {
        await using var db = _fixture.CreateContext();
        return await db.Vouchers.Where(v => v.Code == code).Select(v => v.UsedCount).SingleAsync();
    }

    private static async Task<JsonElement> Data(Task<HttpResponseMessage> request) =>
        await AdminAuthApiClient.ReadDataAsync(await request);

    private static async Task<string?> ErrorOf(Task<HttpResponseMessage> request) =>
        await AuthApiClient.ReadErrorCodeAsync(await request);
}
