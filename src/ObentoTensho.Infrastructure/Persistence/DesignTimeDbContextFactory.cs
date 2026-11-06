using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ObentoTensho.Infrastructure.Persistence;

// Chỉ dùng cho lệnh dotnet ef: chuỗi kết nối lấy từ biến môi trường, không nằm trong mã nguồn.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Đặt biến môi trường ConnectionStrings__Default trước khi chạy dotnet ef.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        return new AppDbContext(options);
    }
}
