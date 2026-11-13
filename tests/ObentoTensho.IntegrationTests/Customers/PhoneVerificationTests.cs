using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Core.Settings.Domain;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;
using ObentoTensho.IntegrationTests.Orders;
using ObentoTensho.IntegrationTests.Reservations;

namespace ObentoTensho.IntegrationTests.Customers;

// Đặt bàn/đặt món cần tài khoản đã xác minh SĐT bằng OTP SMS; một SĐT chỉ một tài khoản xác minh
[Collection(SqlServerFixture.CollectionName)]
public sealed class PhoneVerificationTests : IDisposable
{
    private const string Send = "/api/v1/account/phone/send-code";
    private const string Verify = "/api/v1/account/phone/verify";

    private readonly SqlServerFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _http;
    private readonly AuthApiClient _auth;

    public PhoneVerificationTests(SqlServerFixture fixture)
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

    private static string International(string phone) => "+84" + phone[1..];

    private async Task<string> SignUpAsync(string phone)
    {
        var email = AuthApiClient.NewEmail();
        (await _auth.RegisterAsync(email, phone: phone)).EnsureSuccessStatusCode();
        return await AuthApiClient.ReadAccessTokenAsync(await _auth.LoginAsync(email));
    }

    private Task<HttpResponseMessage> PostAsync(string path, string token, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body ?? new { }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _http.SendAsync(request);
    }

    // Gửi đúng như trình duyệt, không qua VerifiedCustomerHandler
    private Task<HttpResponseMessage> RawBookingAsync(string? token) => RawBookingAsync(_http, token);

    private Task<HttpResponseMessage> RawOrderAsync(string? token, object[] items) => RawOrderAsync(_http, token, items);

    private static Task<HttpResponseMessage> RawBookingAsync(HttpClient http, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/reservations") { Content = JsonContent.Create(ReservationApiClient.Booking(TestSchedule.Tomorrow)) };
        request.Headers.Add(VerifiedCustomerHandler.RawHeader, "1");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return http.SendAsync(request);
    }

    private static Task<HttpResponseMessage> RawOrderAsync(HttpClient http, string? token, object[] items)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders") { Content = JsonContent.Create(OrderApiClient.Order(items)) };
        request.Headers.Add(VerifiedCustomerHandler.RawHeader, "1");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return http.SendAsync(request);
    }

    [Fact]
    public async Task Chua_dang_nhap_hoac_chua_xac_minh_khong_dat_duoc_xac_minh_xong_thi_dat_duoc()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        object[] items = [OrderApiClient.Line(menu.Rice, 1, menu.Small)];
        var phone = AuthApiClient.NewPhone();
        var token = await SignUpAsync(phone);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RawBookingAsync(null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RawOrderAsync(null, items)).StatusCode);
        Assert.Equal(ErrorCodes.PhoneNotVerified, await AuthApiClient.ReadErrorCodeAsync(await RawBookingAsync(token)));
        Assert.Equal(ErrorCodes.PhoneNotVerified, await AuthApiClient.ReadErrorCodeAsync(await RawOrderAsync(token, items)));

        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(Send, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(Verify, token, new { code = _factory.Sms.LastCode(International(phone)) })).StatusCode);

        using var profile = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account/profile");
        profile.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.True((await (await _http.SendAsync(profile)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("data").GetProperty("isPhoneVerified").GetBoolean());

        Assert.Equal(HttpStatusCode.Created, (await RawBookingAsync(token)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await RawOrderAsync(token, items)).StatusCode);
    }

    [Fact]
    public async Task Ma_sai_khong_xac_minh_va_khong_gui_lai_ngay_duoc()
    {
        var phone = AuthApiClient.NewPhone();
        var token = await SignUpAsync(phone);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(Send, token)).StatusCode);
        var code = _factory.Sms.LastCode(International(phone));
        var wrong = string.Equals(code, "000000", StringComparison.Ordinal) ? "000001" : "000000";

        Assert.Equal(ErrorCodes.OtpInvalid, await AuthApiClient.ReadErrorCodeAsync(await PostAsync(Verify, token, new { code = wrong })));
        Assert.Equal(ErrorCodes.RateLimited, await AuthApiClient.ReadErrorCodeAsync(await PostAsync(Send, token)));
        Assert.Single(_factory.Sms.To(International(phone)));

        await using var db = _fixture.CreateContext();
        Assert.False(await db.Users.Where(u => u.PhoneNumber == International(phone)).Select(u => u.IsPhoneVerified).SingleAsync());
    }

    [Fact]
    public async Task Mot_so_chi_mot_tai_khoan_xac_minh()
    {
        var phone = AuthApiClient.NewPhone();
        var first = await SignUpAsync(phone);
        var second = await SignUpAsync(phone);
        await PostAsync(Send, first);
        await PostAsync(Verify, first, new { code = _factory.Sms.LastCode(International(phone)) });

        Assert.Equal(ErrorCodes.PhoneTaken, await AuthApiClient.ReadErrorCodeAsync(await PostAsync(Send, second)));
        Assert.Single(_factory.Sms.To(International(phone)));
    }

    // Chưa có kinh phí SMS: tắt otp.requirePhoneVerify thì đăng ký không cần SĐT, đặt bàn/đặt món chỉ cần email đã xác thực
    [Fact]
    public async Task Tat_bat_buoc_xac_minh_SDT_thi_chi_can_email_da_xac_thuc()
    {
        await TestSchedule.OpenEveryDayAsync(_fixture);
        var menu = await OrderTestMenu.CreateAsync(_fixture);
        object[] items = [OrderApiClient.Line(menu.Rice, 1, menu.Small)];
        using var factory = new ApiFactory(_fixture.ConnectionString, services: s =>
            s.AddSingleton<ISettingsReader>(sp => new PhoneVerifyOff((ISettingsReader)sp.GetRequiredService<ISettingsSnapshot>())));
        using var http = factory.CreateApiClient();
        var auth = new AuthApiClient(http);
        var email = AuthApiClient.NewEmail();

        (await auth.RegisterAsync(email, phone: "")).EnsureSuccessStatusCode();
        var token = await AuthApiClient.ReadAccessTokenAsync(await auth.LoginAsync(email));
        Assert.Equal(ErrorCodes.EmailNotVerified, await AuthApiClient.ReadErrorCodeAsync(await RawBookingAsync(http, token)));
        Assert.Equal(ErrorCodes.EmailNotVerified, await AuthApiClient.ReadErrorCodeAsync(await RawOrderAsync(http, token, items)));

        await using (var db = _fixture.CreateContext())
        {
            Assert.Null(await db.Users.Where(u => u.Email == email).Select(u => u.PhoneNumber).SingleAsync());
            await db.Users.Where(u => u.Email == email).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsEmailVerified, true));
        }

        Assert.Equal(HttpStatusCode.Created, (await RawBookingAsync(http, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await RawOrderAsync(http, token, items)).StatusCode);
    }

    private sealed class PhoneVerifyOff(ISettingsReader inner) : ISettingsReader
    {
        public string Get(string key) => string.Equals(key, SettingKeys.OtpRequirePhoneVerify, StringComparison.Ordinal) ? "false" : inner.Get(key);

        public int GetInt(string key) => inner.GetInt(key);

        public bool GetBool(string key) => !string.Equals(key, SettingKeys.OtpRequirePhoneVerify, StringComparison.Ordinal) && inner.GetBool(key);

        public IReadOnlyDictionary<string, string> Public() => inner.Public();
    }
}
