using System.Net;
using System.Text.Json;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.IntegrationTests.Auth;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Http;

// Enum trên query đọc dạng snake_case giống JSON: trước đây giá trị có dấu gạch dưới (no_show, super_admin, sold_out, in_store) bị trả 400
[Collection(SqlServerFixture.CollectionName)]
public sealed class EnumQueryBindingTests : IDisposable
{
    private const string Admin = "/api/v1/admin";

    private readonly ApiFactory _factory;
    private readonly AdminAuthApiClient _admin;

    public EnumQueryBindingTests(SqlServerFixture fixture)
    {
        _factory = new ApiFactory(fixture.ConnectionString);
        _admin = new AdminAuthApiClient(_factory, fixture);
    }

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("/reservations?status=no_show")]
    [InlineData("/reservations?status=checked_in")]
    [InlineData("/reservations?status=NoShow")]
    [InlineData("/staff?role=content_editor")]
    [InlineData("/menu/items?status=sold_out")]
    [InlineData("/reports/payment-groups/orders?from=2026-11-01&to=2026-11-30&group=in_store")]
    public async Task Gia_tri_co_dau_gach_duoi_doc_duoc(string path)
    {
        var token = await _admin.SignInAsync(StaffRole.SuperAdmin);

        var response = await _admin.SendAsync(HttpMethod.Get, Admin + path, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Loc_vai_tro_super_admin_chi_tra_dung_vai_tro()
    {
        var token = await _admin.SignInAsync(StaffRole.SuperAdmin);

        var response = await _admin.SendAsync(HttpMethod.Get, $"{Admin}/staff?role=super_admin&pageSize=100", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var roles = body.RootElement.GetProperty("data").EnumerateArray().Select(s => s.GetProperty("role").GetString()).ToArray();
        Assert.NotEmpty(roles);
        Assert.All(roles, r => Assert.Equal("super_admin", r));
    }

    [Theory]
    [InlineData("/reservations?status=khac")]
    [InlineData("/reservations?status=1")]
    [InlineData("/staff?role=_")]
    public async Task Gia_tri_la_bi_tu_choi(string path)
    {
        var token = await _admin.SignInAsync(StaffRole.SuperAdmin);

        var response = await _admin.SendAsync(HttpMethod.Get, Admin + path, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
