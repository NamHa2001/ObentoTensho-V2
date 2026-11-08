using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ObentoTensho.Api.Realtime;

// PERF-63: số kết nối hub đang mở, độ trễ từ lúc sự kiện vào hàng chờ (sau commit) tới khi gửi xong, số sự kiện gửi/lỗi.
// Dùng System.Diagnostics.Metrics có sẵn; khi triển khai, OpenTelemetry/dotnet-counters đọc meter "ObentoTensho.Realtime"
public sealed class RealtimeMetrics
{
    public const string MeterName = "ObentoTensho.Realtime";

    private readonly Histogram<double> _delivery;
    private readonly Counter<long> _sent;
    private readonly Counter<long> _failed;

    public RealtimeMetrics(IMeterFactory meters, HubConnectionTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(meters);
        ArgumentNullException.ThrowIfNull(tracker);
        var meter = meters.Create(MeterName);
        meter.CreateObservableGauge("obento.realtime.connections", () => tracker.Count, unit: "{connection}", description: "Số kết nối hub đang mở");
        _delivery = meter.CreateHistogram<double>("obento.realtime.delivery.duration", unit: "ms", description: "Thời gian từ lúc sự kiện vào hàng chờ tới khi gửi xong");
        _sent = meter.CreateCounter<long>("obento.realtime.events.sent", unit: "{event}");
        _failed = meter.CreateCounter<long>("obento.realtime.events.failed", unit: "{event}");
    }

    public void Sent(string eventName, long enqueuedTimestamp)
    {
        var tag = new KeyValuePair<string, object?>("event", eventName);
        _sent.Add(1, tag);
        if (enqueuedTimestamp > 0)
        {
            _delivery.Record(Stopwatch.GetElapsedTime(enqueuedTimestamp).TotalMilliseconds, tag);
        }
    }

    public void Failed(string eventName) => _failed.Add(1, new KeyValuePair<string, object?>("event", eventName));
}
