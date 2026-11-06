using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Infrastructure.Persistence;

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
}
