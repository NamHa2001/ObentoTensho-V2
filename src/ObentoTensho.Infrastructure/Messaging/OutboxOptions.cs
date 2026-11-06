namespace ObentoTensho.Infrastructure.Messaging;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    // Tắt trong test (test gọi bộ xử lý trực tiếp) hoặc trên instance chỉ phục vụ API
    public bool Enabled { get; set; } = true;

    public int BatchSize { get; set; } = 20;

    // BR-NTF-01: tối đa 5 lần thử rồi vào hàng "thất bại"
    public int MaxAttempts { get; set; } = 5;
}
