using Microsoft.Extensions.Caching.Memory;
using ObentoTensho.Core.Content.Abstractions;

namespace ObentoTensho.Infrastructure.Caching;

internal sealed class MemoryPublicContentCache(IMemoryCache cache) : IPublicContentCache
{
    private const string Key = "content:public";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public async Task<PublicContentSnapshot> GetOrCreateAsync(Func<CancellationToken, Task<PublicContentSnapshot>> factory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (cache.TryGetValue(Key, out PublicContentSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        cache.Set(Key, value, Lifetime);
        return value;
    }

    public void Invalidate() => cache.Remove(Key);
}
