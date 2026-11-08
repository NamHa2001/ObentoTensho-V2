using Microsoft.AspNetCore.SignalR;

namespace ObentoTensho.Api.Realtime;

// SEC-69: tối đa 5 kết nối mỗi người dùng (nhiều tab/thiết bị bếp) và 30 mỗi IP; giữ ngữ cảnh để ngắt khi phiên bị thu hồi.
// Đếm trong bộ nhớ tiến trình như các bộ đếm khác (chuyển Redis khi chạy nhiều instance).
public sealed class HubConnectionTracker
{
    public const int MaxPerUser = 5;
    public const int MaxPerIp = 30;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, Entry> _connections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _connections.Count;
            }
        }
    }

    public bool TryAdd(HubCallerContext context, string ipKey, HubIdentity? identity)
    {
        ArgumentNullException.ThrowIfNull(context);
        var keys = identity is null ? new[] { "ip:" + ipKey } : ["ip:" + ipKey, "user:" + identity.Key];
        lock (_lock)
        {
            if (_counts.GetValueOrDefault(keys[0]) >= MaxPerIp || (keys.Length > 1 && _counts.GetValueOrDefault(keys[1]) >= MaxPerUser))
            {
                return false;
            }

            foreach (var key in keys)
            {
                _counts[key] = _counts.GetValueOrDefault(key) + 1;
            }

            _connections[context.ConnectionId] = new Entry(context, identity, keys);
            return true;
        }
    }

    public void Remove(string connectionId)
    {
        lock (_lock)
        {
            if (!_connections.Remove(connectionId, out var entry))
            {
                return;
            }

            foreach (var key in entry.Keys)
            {
                var left = _counts.GetValueOrDefault(key) - 1;
                if (left <= 0)
                {
                    _counts.Remove(key);
                }
                else
                {
                    _counts[key] = left;
                }
            }
        }
    }

    public IReadOnlyList<(HubCallerContext Context, HubIdentity Identity)> Authenticated()
    {
        lock (_lock)
        {
            return _connections.Values.Where(e => e.Identity is not null).Select(e => (e.Context, e.Identity!)).ToArray();
        }
    }

    private sealed record Entry(HubCallerContext Context, HubIdentity? Identity, string[] Keys);
}
