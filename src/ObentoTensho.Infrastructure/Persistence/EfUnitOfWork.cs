using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Common;

namespace ObentoTensho.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    // 2601: trùng chỉ mục duy nhất; 2627: trùng ràng buộc UNIQUE/khóa chính
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            // Bỏ các thay đổi chưa lưu để context dùng tiếp được trong cùng request
            db.ChangeTracker.Clear();
            throw new DuplicateKeyException("Trùng khóa duy nhất.", ex);
        }
    }
}
