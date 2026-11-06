using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Time;
using ObentoTensho.Infrastructure.Persistence;
using ObentoTensho.Infrastructure.Security;
using ObentoTensho.Infrastructure.Time;

namespace ObentoTensho.Infrastructure;

public static class DependencyInjection
{
    private const int CommandTimeoutSeconds = 30;

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // FR-ARC-09: thiếu cấu hình bắt buộc thì dừng ngay khi khởi động
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Thiếu cấu hình ConnectionStrings:Default.");
        }

        // DB-15: tự thử lại khi lỗi tạm thời, thời gian chờ lệnh 30 giây
        services.AddDbContextPool<AppDbContext>(options => options.UseSqlServer(
            connectionString,
            sql => sql.EnableRetryOnFailure().CommandTimeout(CommandTimeoutSeconds)));

        return services;
    }

    public static IServiceCollection AddSecurityFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();

        // Số vòng băm dưới mức tối thiểu thì dừng ngay khi khởi động
        services.AddOptions<PasswordHashingOptions>()
            .Bind(configuration.GetSection(PasswordHashingOptions.SectionName))
            .Validate(
                o => o.Iterations >= PasswordHashingOptions.MinimumIterations,
                $"Security:PasswordHashing:Iterations phải từ {PasswordHashingOptions.MinimumIterations} trở lên.")
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ISecureTokenService, SecureTokenService>();

        return services;
    }
}
