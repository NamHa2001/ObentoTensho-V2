namespace ObentoTensho.Core.Common;

public interface IUnitOfWork
{
    /// <exception cref="DuplicateKeyException">Vi phạm ràng buộc duy nhất.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
