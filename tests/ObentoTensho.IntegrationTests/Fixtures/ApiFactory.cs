using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ObentoTensho.IntegrationTests.Fixtures;

// Chạy toàn bộ API trong bộ nhớ trên CSDL test; khóa ký JWT sinh ngẫu nhiên mỗi lần chạy
public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string CustomerOrigin = "https://www.obento.test";
    public const string AdminOrigin = "https://admin.obento.test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Jwt:ActiveKeyId", "test-1");
        builder.UseSetting("Jwt:SigningKeys:test-1", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Web:CustomerOrigins:0", CustomerOrigin);
        builder.UseSetting("Web:AdminOrigins:0", AdminOrigin);

        // Số vòng tối thiểu cho phép để test chạy nhanh hơn
        builder.UseSetting("Security:PasswordHashing:Iterations", "210000");

        builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly));
    }

    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false,
        });
}
