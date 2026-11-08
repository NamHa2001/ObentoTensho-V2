namespace ObentoTensho.Core.Common;

// Ngày (giờ VN) việc hằng ngày đã chạy xong gần nhất
public interface IJobRunStore
{
    Task<DateOnly?> LastRunDateAsync(string jobName, CancellationToken cancellationToken);

    Task RecordAsync(string jobName, DateOnly localDate, DateTime nowUtc, CancellationToken cancellationToken);
}
