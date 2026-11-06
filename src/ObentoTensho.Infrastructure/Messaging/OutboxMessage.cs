namespace ObentoTensho.Infrastructure.Messaging;

// DB-25, PERF-53: việc cần làm sau khi commit (gửi email…), bộ xử lý nền lấy theo lô
public sealed class OutboxMessage
{
    public long Id { get; private set; }

    public required string Type { get; init; }

    // Có thể chứa token: được xóa (thay bằng "{}") ngay khi tin xử lý xong hoặc thất bại hẳn
    public required string PayloadJson { get; set; }

    public required string IdempotencyKey { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    // Chưa tới giờ này thì chưa lấy (lùi dần khi lỗi, hoặc hoãn sang ngày mai vì hạn mức)
    public required DateTime NextAttemptAtUtc { get; set; }

    // Một worker đã nhận tin tới thời điểm này; worker khác bỏ qua (nhiều instance không xử lý trùng)
    public DateTime? LockedUntilUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    // Hết số lần thử: nằm ở hàng "thất bại" cho quản trị xem (BR-NTF-01)
    public DateTime? FailedAtUtc { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
