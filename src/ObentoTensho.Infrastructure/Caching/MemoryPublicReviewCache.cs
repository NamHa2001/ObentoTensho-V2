using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Core.Content.Abstractions;

namespace ObentoTensho.Infrastructure.Caching;

// Mọi trang gắn chung một token hủy: Invalidate hủy token cũ là bỏ hết các trang cùng lúc
internal sealed class MemoryPublicReviewCache(IMemoryCache cache) : IPublicReviewCache, IDisposable
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private readonly Lock _gate = new();
    private CancellationTokenSource _generation = new();

    public async Task<PagedResponse<PublicReviewDto>> GetOrCreateAsync(string key, Func<CancellationToken, Task<PagedResponse<PublicReviewDto>>> factory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var cacheKey = "reviews:public:" + key;
        if (cache.TryGetValue(cacheKey, out PagedResponse<PublicReviewDto>? cached) && cached is not null)
        {
            return cached;
        }

        // Lấy token trước khi đọc CSDL: nếu có thay đổi trong lúc đọc thì bản vừa đọc bị bỏ ngay
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
