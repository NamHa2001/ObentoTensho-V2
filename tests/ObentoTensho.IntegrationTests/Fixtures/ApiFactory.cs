using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Core.Notifications.Abstractions;
using ObentoTensho.Infrastructure.Messaging;

namespace ObentoTensho.IntegrationTests.Fixtures;

// Chạy toàn bộ API trong bộ nhớ trên CSDL test; khóa ký JWT sinh ngẫu nhiên mỗi lần chạy.
// settings ghi đè cấu hình (vd hạ giới hạn tần suất cho test 429).
public sealed class ApiFactory(string connectionString, IReadOnlyDictionary<string, string>? settings = null)
    : WebApplicationFactory<Program>
{
    public const string CustomerOrigin = "https://www.obento.test";
    public const string AdminOrigin = "https://admin.obento.test";

    // Thư "gửi" trong test nằm ở đây; hộp thư đi không tự chạy, test gọi ProcessOutboxAsync
    public FakeEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Jwt:ActiveKeyId", "test-1");
        builder.UseSetting("Jwt:SigningKeys:test-1", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Web:CustomerOrigins:0", CustomerOrigin);
        builder.UseSetting("Web:AdminOrigins:0", AdminOrigin);
        builder.UseSetting("Web:PublicBaseUrl", CustomerOrigin);
        builder.UseSetting("Web:AdminBaseUrl", AdminOrigin);
        builder.UseSetting("Email:FromAddress", "no-reply@mail.obento.test");
        builder.UseSetting("Email:ReplyTo", "lienhe@obento.test");
        builder.UseSetting("Outbox:Enabled", "false");

        // Số vòng tối thiểu cho phép để test chạy nhanh hơn
        builder.UseSetting("Security:PasswordHashing:Iterations", "210000");

        // Các test chức năng gọi đăng nhập nhiều lần từ cùng "IP"; test giới hạn tần suất tự hạ lại
        builder.UseSetting("RateLimiting:LoginPerMinute", "1000");
        builder.UseSetting("RateLimiting:GeneralPerMinute", "10000");

        foreach (var (key, value) in settings ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);
            services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, TestRemoteIpStartupFilter>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    // Chạy hộp thư đi tới khi hết tin tới hạn (gồm cả tin còn sót của test khác — vô hại)
    public async Task ProcessOutboxAsync()
    {
        var processor = Services.GetRequiredService<OutboxProcessor>();
        while (await processor.ProcessBatchAsync(CancellationToken.None) > 0)
        {
        }
    }

    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false,
        });
}
