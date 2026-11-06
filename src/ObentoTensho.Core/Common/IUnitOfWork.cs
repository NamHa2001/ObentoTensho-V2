namespace ObentoTensho.Core.Common;

public interface IUnitOfWork
{
    /// <exception cref="DuplicateKeyException">Vi phạm ràng buộc duy nhất.</exception>
    /// <exception cref="ConcurrencyConflictException">Bản ghi đã bị người khác sửa/xóa.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    // BR-TBL-16: so RowVersion client gửi (ETag) với bản trong CSDL khi lưu
    void ExpectRowVersion(object entity, byte[] rowVersion);

    // Nhiều lần lưu trong một transaction (vd: tạo bản ghi lấy Id rồi ghi audit — BR-STF-12)
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}
