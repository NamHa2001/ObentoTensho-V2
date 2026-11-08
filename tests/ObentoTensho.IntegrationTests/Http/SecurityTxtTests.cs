using System.Net;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Http;

[Collection(SqlServerFixture.CollectionName)]
public sealed class SecurityTxtTests(SqlServerFixture fixture)
{
    private const string Path = "/.well-known/security.txt";

    [Fact]
    public async Task Chua_cau_hinh_email_thi_404()
    {
        using var factory = new ApiFactory(fixture.ConnectionString);
        using var http = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(Path)).StatusCode);
    }

    [Fact]
    public async Task Co_email_thi_tra_Contact_va_Expires_con_han()
    {
        using var factory = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string>(StringComparer.Ordinal) { ["Web:SecurityContact"] = "baomat@obento.test" });
        using var http = factory.CreateApiClient();
        var response = await http.GetAsync(Path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        var lines = (await response.Content.ReadAsStringAsync()).Split('\n');
        Assert.Equal("Contact: mailto:baomat@obento.test", lines[0]);
        var expires = DateTime.Parse(lines[1]["Expires: ".Length..], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.True(expires > DateTime.UtcNow.AddDays(30));
    }
}
