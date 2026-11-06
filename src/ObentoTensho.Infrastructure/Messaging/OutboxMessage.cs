namespace ObentoTensho.Infrastructure.Messaging;

// DB-25, PERF-53: việc cần làm sau khi commit (gửi email…), bộ xử lý nền lấy theo lô
public sealed class OutboxMessage
{
    public long Id { get; private set; }

    public required string Type { get; init; }

    public required string PayloadJson { get; init; }

    public required string IdempotencyKey { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? ProcessedAtUtc { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
