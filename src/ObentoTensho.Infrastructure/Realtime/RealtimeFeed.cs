using System.Diagnostics;
using System.Threading.Channels;
using ObentoTensho.Core.Realtime.Domain;

namespace ObentoTensho.Infrastructure.Realtime;

// Sự kiện đã commit chờ gửi. Đầy thì bỏ cái cũ nhất: client luôn đồng bộ lại qua REST khi nối lại (RT-06),
// nên mất một sự kiện không làm sai dữ liệu
public sealed class RealtimeFeed
{
    private const int Capacity = 10_000;

    private readonly Channel<RealtimeMessage> _channel = Channel.CreateBounded<RealtimeMessage>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
    });

    public ChannelReader<RealtimeMessage> Reader => _channel.Reader;

    public void Enqueue(RealtimeMessage message) =>
        _channel.Writer.TryWrite(message with { EnqueuedTimestamp = Stopwatch.GetTimestamp() });
}
