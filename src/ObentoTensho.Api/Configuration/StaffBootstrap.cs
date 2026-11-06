using ObentoTensho.Core.Auth.Services;

namespace ObentoTensho.Api.Configuration;

public static partial class StaffBootstrap
{
    // SEC-15: Seed__SuperAdmin__Email/FullName/Password qua biến môi trường; chỉ tạo khi chưa có SuperAdmin nào
    public static async Task SeedSuperAdminAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var section = app.Configuration.GetSection("Seed:SuperAdmin");
        var email = section["Email"];
        var password = section["Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<StaffBootstrapService>();
        if (await bootstrap.EnsureSuperAdminAsync(email, section["FullName"] ?? "Quản trị viên", password, CancellationToken.None))
        {
            LogSuperAdminCreated(app.Logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Đã tạo SuperAdmin đầu tiên; hãy xóa Seed:SuperAdmin:Password khỏi cấu hình.")]
    private static partial void LogSuperAdminCreated(ILogger logger);
}
