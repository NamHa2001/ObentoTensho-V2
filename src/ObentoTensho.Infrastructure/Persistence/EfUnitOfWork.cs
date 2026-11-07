using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Common;
using ObentoTensho.Infrastructure.Realtime;

namespace ObentoTensho.Infrastructure.Persistence;

// Sự kiện realtime gom trong phạm vi chỉ phát sau commit (DB-25); lưu lỗi, hoàn tác hay lần thử bị hủy thì bỏ
internal sealed class EfUnitOfWork(AppDbContext db, PendingRealtimeEvents realtime) : IUnitOfWork
{
    // 2601: trùng chỉ mục duy nhất; 2627: trùng ràng buộc UNIQUE/khóa chính
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;
    private const string RowVersionProperty = "RowVersion";

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var standalone = db.Database.CurrentTransaction is null;
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch
        {
            if (standalone)
            {
                realtime.Discard();
            }

            throw;
        }

        // Ngoài transaction thì lưu xong là đã commit
        if (standalone)
        {
            realtime.Publish();
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
            realtime.Discard();
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var result = await work(ct);
                await transaction.CommitAsync(ct);
                realtime.Publish();
                return result;
            }
            finally
            {
                realtime.Discard();
            }
        }, cancellationToken);

    public Task<Result<T>> TryInTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            db.ChangeTracker.Clear();
            realtime.Discard();
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var result = await work(ct);
                if (result.IsSuccess)
                {
                    await transaction.CommitAsync(ct);
                    realtime.Publish();
                }
                else
                {
                    await transaction.RollbackAsync(ct);
                    db.ChangeTracker.Clear();
                }

                return result;
            }
            finally
            {
                realtime.Discard();
            }
        }, cancellationToken);

    private async Task SaveAsync(CancellationToken cancellationToken)
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
}
