using Microsoft.Extensions.Caching.Memory;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Services;

namespace ObentoTensho.Infrastructure.Caching;

// PERF-10: một ảnh chụp thực đơn trong bộ nhớ mỗi tiến trình; sửa ở trang quản trị gọi Invalidate (BR-MNU-09)
internal sealed class MemoryMenuCache(IMemoryCache cache) : IMenuCache
{
    private const string Key = "menu:public-snapshot";

    // Trạng thái "hết đến giờ X" tính theo giờ lúc đọc; hết hạn chỉ để phòng dữ liệu bị sửa ngoài ứng dụng
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public async Task<MenuSnapshot> GetOrCreateAsync(Func<CancellationToken, Task<MenuSnapshot>> factory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (cache.TryGetValue(Key, out MenuSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        cache.Set(Key, value, Lifetime);
        return value;
    }

    public void Invalidate() => cache.Remove(Key);
}
