using Microsoft.Extensions.Caching.Memory;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Core.Tables.Abstractions;

namespace ObentoTensho.Infrastructure.Caching;

// PERF-11: một bản trong bộ nhớ mỗi tiến trình; nhiều instance thì chuyển sang HybridCache + Redis ở phần triển khai
internal sealed class MemoryPublicScheduleCache(IMemoryCache cache) : IPublicScheduleCache
{
    private const string Key = "schedule:public-opening-hours";

    // Hết hạn ngắn để danh sách "ngày đặc biệt sắp tới" tự trượt theo ngày mà không cần job
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public async Task<PublicOpeningHoursDto> GetOrCreateAsync(Func<CancellationToken, Task<PublicOpeningHoursDto>> factory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (cache.TryGetValue(Key, out PublicOpeningHoursDto? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        cache.Set(Key, value, Lifetime);
        return value;
    }

    public void Invalidate() => cache.Remove(Key);
}
