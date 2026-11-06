using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Common;

namespace ObentoTensho.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    // 2601: trùng chỉ mục duy nhất; 2627: trùng ràng buộc UNIQUE/khóa chính
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;
    private const string RowVersionProperty = "RowVersion";

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            db.ChangeTracker.Clear();
            throw new ConcurrencyConflictException("Bản ghi đã bị thay đổi.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            // Bỏ các thay đổi chưa lưu để context dùng tiếp được trong cùng request
            db.ChangeTracker.Clear();
            throw new DuplicateKeyException("Trùng khóa duy nhất.", ex);
        }
    }

    public void ExpectRowVersion(object entity, byte[] rowVersion) =>
        db.Entry(entity).Property(RowVersionProperty).OriginalValue = rowVersion;

    // Chạy qua execution strategy vì DbContext bật thử lại khi lỗi tạm thời (DB-15):
    // mỗi lần thử bắt đầu từ context sạch nên `work` phải tự nạp/tạo mọi thực thể nó cần
    public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var result = await work(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
}
