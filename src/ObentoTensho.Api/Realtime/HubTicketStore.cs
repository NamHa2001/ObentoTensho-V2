using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ObentoTensho.Api.Realtime;

// RT-09, SEC-46: vé ngẫu nhiên 256 bit, sống 30 giây, đổi được một lần; access token không bao giờ nằm trên URL.
// Vé ở bộ nhớ tiến trình: chạy nhiều instance thì chuyển sang Redis cùng các bộ đếm khác (VIEC_CON_NO).
public sealed class HubTicketStore(TimeProvider time)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);
    private const int MaxOutstanding = 50_000;

    private readonly ConcurrentDictionary<string, (HubIdentity Identity, DateTimeOffset ExpiresAt)> _tickets = new(StringComparer.Ordinal);

    public (string Ticket, DateTimeOffset ExpiresAt)? Issue(HubIdentity identity)
    {
        var now = time.GetUtcNow();
        foreach (var expired in _tickets.Where(t => t.Value.ExpiresAt <= now).Select(t => t.Key).ToArray())
        {
            _tickets.TryRemove(expired, out _);
        }

        if (_tickets.Count >= MaxOutstanding)
        {
            return null;
        }

        var ticket = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expiresAt = now + Lifetime;
        _tickets[ticket] = (identity, expiresAt);
        return (ticket, expiresAt);
    }

    // Bước negotiate của SignalR chỉ xem vé; request mở kết nối mới tiêu vé
    public HubIdentity? Peek(string ticket) =>
        _tickets.TryGetValue(ticket, out var entry) && entry.ExpiresAt > time.GetUtcNow() ? entry.Identity : null;

    public HubIdentity? Redeem(string ticket) =>
        _tickets.TryRemove(ticket, out var entry) && entry.ExpiresAt > time.GetUtcNow() ? entry.Identity : null;
}
