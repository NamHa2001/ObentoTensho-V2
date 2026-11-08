namespace ObentoTensho.Infrastructure.Jobs;

// Lần chạy xong gần nhất của việc hằng ngày
public sealed class JobRun
{
    public required string Name { get; init; }

    public required DateOnly LastRunLocalDate { get; set; }

    public required DateTime LastRunAtUtc { get; set; }
}
