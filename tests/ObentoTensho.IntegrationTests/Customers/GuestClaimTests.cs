using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Settings.Domain;
using ObentoTensho.Core.Settings.Services;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;
using ObentoTensho.IntegrationTests.Reservations;

namespace ObentoTensho.IntegrationTests.Customers;

// BR-AUTH-17: gắn đơn/đặt bàn vãng lai vào tài khoản bằng mã + token trong thư gửi tới chính email đã xác thực
[Collection(SqlServerFixture.CollectionName)]
public sealed class GuestClaimTests : IDisposable
{
    private const string Account = "/api/v1/account";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AuthApiClient _auth;

    public GuestClaimTests(SqlServerFixture fixture)
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

    private async Task<(Guid Id, string Access)> CustomerAsync(string email, bool verified = true)
    {
        var (access, _) = await _auth.RegisterAndLoginAsync(email);
        await using var db = _fixture.CreateContext();
        var id = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsEmailVerified, verified));
        return (id, access);
    }

    private async Task<HttpResponseMessage> ClaimAsync(string kind, string access, string code, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Account}/claim-{kind}") { Content = JsonContent.Create(new { code, token }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return await _http.SendAsync(request);
    }

    private async Task<(string Code, string Token)> GuestOrderAsync(OrderTestMenu menu, string email)
    {
        var created = await new OrderApiClient(_http).CreateAsync(OrderApiClient.Order([OrderApiClient.Line(menu.Rice, 2, menu.Small)], email: email));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var data = await OrderApiClient.DataOf(created);
        return (data.GetProperty("order").GetProperty("code").GetString()!, data.GetProperty("accessToken").GetString()!);
    }

    private async Task SetClaimPointsAsync(bool on)
    {
        await using (var db = _fixture.CreateContext())
        {
            await db.SystemSettings.Where(s => s.Key == SettingKeys.LoyaltyClaimGuestOrders).ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, on ? "true" : "false"));
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SettingsSeeder>().RefreshAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Gan_don_vang_lai_can_email_xac_thuc_dung_token_va_chi_cong_diem_khi_bat()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var email = AuthApiClient.NewEmail();
        var pending = await GuestOrderAsync(menu, email);
        var completed = await GuestOrderAsync(menu, email.ToUpperInvariant());
        var other = await GuestOrderAsync(menu, AuthApiClient.NewEmail());
        await using (var db = _fixture.CreateContext())
        {
            await db.Orders.Where(o => o.OrderCode == completed.Code).ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Completed)
                .SetProperty(o => o.PaymentStatus, PaymentStatus.Paid));
        }

        var (unverifiedId, unverified) = await CustomerAsync(email, verified: false);
        Assert.Equal(ErrorCodes.EmailNotVerified, await AuthApiClient.ReadErrorCodeAsync(await ClaimAsync("order", unverified, pending.Code, pending.Token)));
        await using (var db = _fixture.CreateContext())
        {
            // Tài khoản chưa xác thực nhường email cho tài khoản thật
            await db.Users.Where(u => u.Id == unverifiedId).ExecuteDeleteAsync();
        }

        var (id, access) = await CustomerAsync(email);

        // Sai token, đơn của email khác, mã không tồn tại: cùng NOT_FOUND
        Assert.Equal(ErrorCodes.NotFound, await AuthApiClient.ReadErrorCodeAsync(await ClaimAsync("order", access, pending.Code, other.Token)));
        Assert.Equal(ErrorCodes.NotFound, await AuthApiClient.ReadErrorCodeAsync(await ClaimAsync("order", access, other.Code, other.Token)));
        Assert.Equal(ErrorCodes.NotFound, await AuthApiClient.ReadErrorCodeAsync(await ClaimAsync("order", access, "OT-000000-XXXX", pending.Token)));

        Assert.Equal(HttpStatusCode.NoContent, (await ClaimAsync("order", access, pending.Code.ToLowerInvariant(), pending.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimAsync("order", access, pending.Code, pending.Token)).StatusCode);

        // Cờ tắt (mặc định): gắn đơn đã hoàn thành nhưng không cộng điểm hồi tố
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimAsync("order", access, completed.Code, completed.Token)).StatusCode);
        await using (var db = _fixture.CreateContext())
        {
            Assert.Equal(2, await db.Orders.CountAsync(o => o.UserId == id));
            Assert.Equal(0, await db.Orders.Where(o => o.OrderCode == completed.Code).Select(o => o.PointsEarned).SingleAsync());
            Assert.False(await db.LoyaltyPoints.AnyAsync(p => p.UserId == id));
            var ids = await db.Orders.Where(o => o.UserId == id).Select(o => o.Id.ToString()).ToListAsync();
            Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "customers.guest_order.claimed" && ids.Contains(a.EntityId) && a.ActorId == id));
        }

        // Người khác có token cũng không lấy được đơn đã có chủ
        var stranger = await CustomerAsync(AuthApiClient.NewEmail());
        Assert.Equal(ErrorCodes.NotFound, await AuthApiClient.ReadErrorCodeAsync(await ClaimAsync("order", stranger.Access, pending.Code, pending.Token)));
    }

    [Fact]
    public async Task Bat_cau_hinh_thi_don_hoan_thanh_duoc_cong_diem_hoi_to_mot_lan()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        var email = AuthApiClient.NewEmail();
        var pending = await GuestOrderAsync(menu, email);
        var later = await GuestOrderAsync(menu, email);
        var (id, access) = await CustomerAsync(email);

        // Đơn đã thuộc tài khoản khác (vd. chủ cũ của email) thì không chuyển chủ dù đúng email và token
        var owned = await GuestOrderAsync(menu, email);
        var (otherOwner, _) = await CustomerAsync(AuthApiClient.NewEmail());
        await using (var db = _fixture.CreateContext())
        {
            await db.Orders.Where(o => o.OrderCode == owned.Code).ExecuteUpdateAsync(s => s.SetProperty(o => o.UserId, otherOwner));
        }

        Assert.Equal(ErrorCodes.NotFound, await AuthApiClient.ReadErrorCodeAsync(await ClaimAsync("order", access, owned.Code, owned.Token)));
        await using (var db = _fixture.CreateContext())
        {
            await db.Orders.Where(o => o.OrderCode == later.Code).ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Completed)
                .SetProperty(o => o.PaymentStatus, PaymentStatus.Paid));
        }

        await SetClaimPointsAsync(on: true);
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await ClaimAsync("order", access, pending.Code, pending.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await ClaimAsync("order", access, later.Code, later.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await ClaimAsync("order", access, later.Code, later.Token)).StatusCode);
            await using var db = _fixture.CreateContext();
            var earned = await db.Orders.Where(o => o.OrderCode == later.Code).Select(o => o.PointsEarned).SingleAsync();
            Assert.True(earned > 0);
            Assert.Equal(earned, await db.LoyaltyPoints.Where(p => p.UserId == id).SumAsync(p => p.Points));
            Assert.Equal(0, await db.Orders.Where(o => o.OrderCode == pending.Code).Select(o => o.PointsEarned).SingleAsync());
        }
        finally
        {
            await SetClaimPointsAsync(on: false);
        }
    }

    [Fact]
    public async Task Gan_dat_ban_vang_lai_va_khoa_khi_do_lien_tuc()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var email = AuthApiClient.NewEmail();
        var reservations = new ReservationApiClient(_http);
        var created = await reservations.CreateAsync(ReservationApiClient.Booking(TestSchedule.Tomorrow, email: email));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var code = await ReservationApiClient.CodeOf(created);
        await _factory.ProcessOutboxAsync();
        var token = ReservationApiClient.TokenIn(_factory.Emails, email);

        var (id, access) = await CustomerAsync(email);
        Assert.Equal(ErrorCodes.NotFound, await AuthApiClient.ReadErrorCodeAsync(await ClaimAsync("reservation", access, code, token + "x")));
        Assert.Equal(HttpStatusCode.NoContent, (await ClaimAsync("reservation", access, code, token)).StatusCode);
        await using (var db = _fixture.CreateContext())
        {
            Assert.Equal(id, await db.Reservations.Where(r => r.ReservationCode == code).Select(r => r.UserId).SingleAsync());
            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "customers.guest_reservation.claimed"));
        }

        // Đã có chủ khác thì không chuyển, dù đúng email và token
        var (otherOwner, _) = await CustomerAsync(AuthApiClient.NewEmail());
        await using (var db = _fixture.CreateContext())
        {
            await db.Reservations.Where(r => r.ReservationCode == code).ExecuteUpdateAsync(s => s.SetProperty(r => r.UserId, otherOwner));
        }

        Assert.Equal(ErrorCodes.NotFound, await AuthApiClient.ReadErrorCodeAsync(await ClaimAsync("reservation", access, code, token)));

        // Dò mã/token: sau 10 lần sai bị chặn, kể cả khi gửi đúng
        var (_, prober) = await CustomerAsync(AuthApiClient.NewEmail());
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(ErrorCodes.NotFound, await AuthApiClient.ReadErrorCodeAsync(await ClaimAsync("reservation", prober, code, $"sai-{i}")));
        }

        Assert.Equal(ErrorCodes.RateLimited, await AuthApiClient.ReadErrorCodeAsync(await ClaimAsync("order", prober, code, token)));
        Assert.Equal(ErrorCodes.RateLimited, await AuthApiClient.ReadErrorCodeAsync(await ClaimAsync("reservation", prober, code, token)));
    }
}
