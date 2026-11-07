namespace ObentoTensho.Core.Realtime.Domain;

public sealed record RealtimeMessage(string Event, IReadOnlyList<string> Groups, object Payload)
{
    // Cùng một thay đổi có thể được ghi nhận ở nhiều tầng (vd lễ tân đổi trạng thái) — chỉ phát một lần
    public bool SameAs(RealtimeMessage other) =>
        other is not null
        && string.Equals(Event, other.Event, StringComparison.Ordinal)
        && Payload.Equals(other.Payload)
        && Groups.SequenceEqual(other.Groups, StringComparer.Ordinal);
}
