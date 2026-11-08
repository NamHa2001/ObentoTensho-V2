using Microsoft.Extensions.Caching.Memory;
using ObentoTensho.Core.Common;

namespace ObentoTensho.Infrastructure.Caching;

// Bộ đếm trong bộ nhớ từng tiến trình; nhiều instance thì chuyển sang Redis ở phần triển khai
internal sealed class MemoryActionThrottle(IMemoryCache cache) : IActionThrottle
{
    private readonly Lock _gate = new();

    public int Count(string key) => cache.TryGetValue(CacheKey(key), out Counter? counter) && counter is not null ? counter.Value : 0;

    public int Increment(string key, TimeSpan window)
    {
        lock (_gate)
        {
            if (cache.TryGetValue(CacheKey(key), out Counter? counter) && counter is not null)
            {
                counter.Value++;
                return counter.Value;
            }

            cache.Set(CacheKey(key), new Counter { Value = 1 }, window);
            return 1;
        }
    }

    public void Reset(string key)
    {
        lock (_gate)
        {
            cache.Remove(CacheKey(key));
        }
    }

    private static string CacheKey(string key) => "throttle:" + key;

    private sealed class Counter
    {
        public int Value { get; set; }
    }
}
