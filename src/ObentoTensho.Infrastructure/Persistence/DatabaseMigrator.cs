using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ObentoTensho.Infrastructure.Persistence;

// Host dùng chung (MonsterASP) chỉ cho CSDL kết nối từ chính máy chủ, không chạy migration từ máy khác được:
// bật Database:MigrateOnStartup để API tự áp dụng migration còn thiếu khi khởi động. Máy chủ riêng vẫn chạy migration tay.
public static class DatabaseMigrator
{
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
