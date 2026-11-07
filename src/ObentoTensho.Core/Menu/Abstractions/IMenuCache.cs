using ObentoTensho.Core.Menu.Services;

namespace ObentoTensho.Core.Menu.Abstractions;

// PERF-10, BR-MNU-09: thực đơn công khai cache; mọi thay đổi ở trang quản trị xóa cache ngay
public interface IMenuCache
{
    Task<MenuSnapshot> GetOrCreateAsync(Func<CancellationToken, Task<MenuSnapshot>> factory, CancellationToken cancellationToken);

    void Invalidate();
}
