using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Auth.Abstractions;

namespace ObentoTensho.Infrastructure.Security;

// Trong bộ nhớ tiến trình như các bộ đếm khác; chạy nhiều instance thì chuyển sang Redis (VIEC_CON_NO)
internal sealed class MemorySessionDenylist(IMemoryCache cache, IOptions<JwtOptions> options) : ISessionDenylist
{
    public TimeSpan Lifetime => TimeSpan.FromMinutes(options.Value.AccessTokenMinutes) + JwtOptions.ClockSkew + TimeSpan.FromMinutes(1);

    public void RevokeSession(Guid sessionId) => cache.Set(Key(sessionId), true, Lifetime);

    public bool IsRevoked(Guid sessionId) => cache.TryGetValue(Key(sessionId), out _);

    private static string Key(Guid id) => "denylist:session:" + id.ToString("N");
}
