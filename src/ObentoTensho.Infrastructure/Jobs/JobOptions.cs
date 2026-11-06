namespace ObentoTensho.Infrastructure.Jobs;

public sealed class JobOptions
{
    public const string SectionName = "Jobs";

    // Tắt trong test (test gọi việc trực tiếp) hoặc trên instance chỉ phục vụ API
    public bool Enabled { get; set; } = true;

    // Nhịp kiểm tra việc nào tới hạn; mỗi việc vẫn chạy theo Interval riêng
    public int TickSeconds { get; set; } = 30;
}
