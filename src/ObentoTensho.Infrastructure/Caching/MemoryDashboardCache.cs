using Microsoft.Extensions.Caching.Memory;
using ObentoTensho.Core.Reports.Abstractions;

namespace ObentoTensho.Infrastructure.Caching;

internal sealed class MemoryDashboardCache(IMemoryCache cache) : IDashboardCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(45);

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var cacheKey = $"dashboard:{key}";
        if (cache.TryGetValue(cacheKey, out T? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        cache.Set(cacheKey, value, Lifetime);
        return value;
    }
}
