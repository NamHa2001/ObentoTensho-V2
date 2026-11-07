using ObentoTensho.Core.Realtime.Abstractions;
using ObentoTensho.Core.Realtime.Domain;

namespace ObentoTensho.Infrastructure.Realtime;

// Một phạm vi (request/job) gom sự kiện; EfUnitOfWork đẩy sang hàng đợi khi commit, bỏ khi hoàn tác/thử lại (DB-25)
public sealed class PendingRealtimeEvents(RealtimeFeed queue) : IRealtimeEvents
{
    private readonly List<RealtimeMessage> _pending = [];

    public void Stage(RealtimeMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!_pending.Exists(m => m.SameAs(message)))
        {
            _pending.Add(message);
        }
    }

    internal void Discard() => _pending.Clear();

    internal void Publish()
    {
        foreach (var message in _pending)
        {
            queue.Enqueue(message);
        }

        _pending.Clear();
    }
}
