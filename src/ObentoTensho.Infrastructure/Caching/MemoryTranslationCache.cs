using Microsoft.Extensions.Caching.Memory;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Localization.Abstractions;
using ObentoTensho.Core.Localization.Domain;

namespace ObentoTensho.Infrastructure.Caching;

// Khóa có số thế hệ: lưu bản dịch nào thì tăng thế hệ, mọi khóa cũ hết dùng ngay và tự hết hạn sau
internal sealed class MemoryTranslationCache(IMemoryCache cache) : ITranslationCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private long _generation;

    public async Task<IReadOnlyList<ContentTranslation>> GetOrCreateAsync(
        TranslatableContent kind,
        string lang,
        Func<CancellationToken, Task<IReadOnlyList<ContentTranslation>>> factory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var key = $"translations:{Interlocked.Read(ref _generation)}:{kind}:{lang}";
        if (cache.TryGetValue(key, out IReadOnlyList<ContentTranslation>? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        cache.Set(key, value, Lifetime);
        return value;
    }

    public void Invalidate() => Interlocked.Increment(ref _generation);
}
