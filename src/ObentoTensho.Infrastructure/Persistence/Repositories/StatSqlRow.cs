namespace ObentoTensho.Infrastructure.Persistence.Repositories;

// Kết quả truy vấn gom nhóm thô
internal sealed class StatSqlRow
{
    public string Metric { get; set; } = string.Empty;

    public string Dimension { get; set; } = string.Empty;

    public decimal Value { get; set; }
}
