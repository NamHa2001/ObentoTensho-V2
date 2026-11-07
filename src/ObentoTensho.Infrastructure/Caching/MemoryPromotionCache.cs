using Microsoft.Extensions.Caching.Memory;
using ObentoTensho.Core.Promotions.Abstractions;

namespace ObentoTensho.Infrastructure.Caching;

// PERF-12: một bản trong bộ nhớ mỗi tiến trình; ngày hiệu lực lọc lúc đọc nên cache không làm sai trạng thái khuyến mãi
internal sealed class MemoryPromotionCache(IMemoryCache cache) : IPromotionCache
{
    private const string Key = "promotions:public";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyList<PublicPromotionEntry>> GetOrCreateAsync(Func<CancellationToken, Task<IReadOnlyList<PublicPromotionEntry>>> factory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (cache.TryGetValue(Key, out IReadOnlyList<PublicPromotionEntry>? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        cache.Set(Key, value, Lifetime);
        return value;
    }

    public void Invalidate() => cache.Remove(Key);
}
