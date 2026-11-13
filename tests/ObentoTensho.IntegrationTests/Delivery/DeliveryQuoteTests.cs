using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Delivery;

// Xem trước phí ship: quãng đường thật từ dịch vụ bản đồ, miễn phí 2 km, 15.000đ/km sau đó, tối đa 7 km
[Collection(SqlServerFixture.CollectionName)]
public sealed class DeliveryQuoteTests(SqlServerFixture fixture)
{
    private static Task<HttpResponseMessage> QuoteAsync(HttpClient http, string address) =>
        http.PostAsJsonAsync("/api/v1/orders/delivery-quote", new { address });

    [Fact]
    public async Task Tinh_phi_theo_quang_duong_va_chan_ngoai_pham_vi()
    {
        var routes = new DeliveryTestHost.FakeRoutes();
        using var factory = DeliveryTestHost.Create(fixture.ConnectionString, routes);
        using var http = factory.CreateApiClient();

        var near = await (await QuoteAsync(http, "12 Lê Lợi 1800m Quận 1")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, near.GetProperty("data").GetProperty("fee").GetDecimal());

        var far = await (await QuoteAsync(http, "88 Nguyễn Huệ 4100m Quận 1")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(45_000, far.GetProperty("data").GetProperty("fee").GetDecimal());
        Assert.Equal(4.1m, far.GetProperty("data").GetProperty("distanceKm").GetDecimal());

        Assert.Equal(ErrorCodes.DeliveryTooFar, await AuthApiClient.ReadErrorCodeAsync(await QuoteAsync(http, "Thủ Đức xa lắm 7001m")));
        Assert.Equal(ErrorCodes.AddressNotFound, await AuthApiClient.ReadErrorCodeAsync(await QuoteAsync(http, "địa chỉ khong-tim-thay")));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await QuoteAsync(http, "123 đường google-loi")).StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, await AuthApiClient.ReadErrorCodeAsync(await QuoteAsync(http, "ngắn")));
    }

    [Fact]
    public async Task Quan_chua_bat_giao_hang_thi_tu_choi_va_khong_goi_ban_do()
    {
        var routes = new DeliveryTestHost.FakeRoutes();
        using var factory = DeliveryTestHost.Create(fixture.ConnectionString, routes, enabled: false);
        using var http = factory.CreateApiClient();

        Assert.Equal(ErrorCodes.DeliveryDisabled, await AuthApiClient.ReadErrorCodeAsync(await QuoteAsync(http, "12 Lê Lợi 1800m Quận 1")));
        Assert.Equal(0, routes.Calls);
    }
}
