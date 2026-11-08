using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Core.Tables.Abstractions;

namespace ObentoTensho.Infrastructure.Caching;

// PERF-11: một bản trong bộ nhớ mỗi tiến trình; nhiều instance thì chuyển sang HybridCache + Redis ở phần triển khai.
// Mọi mục gắn chung một token hủy: Invalidate hủy token cũ là bỏ hết cùng lúc
internal sealed class MemoryPublicScheduleCache(IMemoryCache cache) : IPublicScheduleCache, IDisposable
{
    private const string PublicKey = "public-opening-hours";

    // Hết hạn ngắn để danh sách "ngày đặc biệt sắp tới" tự trượt theo ngày mà không cần job
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly Lock _gate = new();
    private CancellationTokenSource _generation = new();

    public Task<PublicOpeningHoursDto> GetOrCreateAsync(Func<CancellationToken, Task<PublicOpeningHoursDto>> factory, CancellationToken cancellationToken) =>
        GetOrCreateAsync(PublicKey, factory, cancellationToken);

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        var cacheKey = "schedule:" + key;
        if (cache.TryGetValue(cacheKey, out T? cached) && cached is not null)
        {
            return cached;
        }

        // Lấy token trước khi đọc CSDL: có thay đổi trong lúc đọc thì bản vừa đọc bị bỏ ngay
        CancellationToken generation;
        lock (_gate)
        {
            generation = _generation.Token;
        }

        var value = await factory(cancellationToken);
        using var entry = cache.CreateEntry(cacheKey);
        entry.Value = value;
        entry.AbsoluteExpirationRelativeToNow = Lifetime;
        entry.AddExpirationToken(new CancellationChangeToken(generation));
        return value;
    }

    public void Invalidate()
    {
        CancellationTokenSource old;
        lock (_gate)
        {
            old = _generation;
            _generation = new CancellationTokenSource();
        }

        old.Cancel();
        old.Dispose();
    }

    public void Dispose() => _generation.Dispose();
}
