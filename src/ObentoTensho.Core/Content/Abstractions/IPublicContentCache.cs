namespace ObentoTensho.Core.Content.Abstractions;

// PERF-11: nội dung công khai cache ngắn, xóa ngay khi quản trị sửa
public interface IPublicContentCache
{
    Task<PublicContentSnapshot> GetOrCreateAsync(Func<CancellationToken, Task<PublicContentSnapshot>> factory, CancellationToken cancellationToken);

    void Invalidate();
}
