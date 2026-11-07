namespace ObentoTensho.Core.Reports.Domain;

// docs/srs/08 DailyStats (PERF-55): Date là ngày kinh doanh VN; Dimension rỗng = tổng
public sealed class DailyStat
{
    public required DateOnly Date { get; init; }

    public required string Metric { get; init; }

    public required string Dimension { get; init; }

    public required decimal Value { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}
