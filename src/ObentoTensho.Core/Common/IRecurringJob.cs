namespace ObentoTensho.Core.Common;

// Việc chạy định kỳ (docs/srs/20 §4). Phải idempotent: chạy hai lần liên tiếp không gây tác dụng hai lần.
public interface IRecurringJob
{
    string Name { get; }

    TimeSpan Interval { get; }

    // Trả số bản ghi đã xử lý để ghi log đầu/cuối
    Task<int> RunAsync(CancellationToken cancellationToken);
}
