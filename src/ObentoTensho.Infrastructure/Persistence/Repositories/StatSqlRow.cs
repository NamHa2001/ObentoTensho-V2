using Microsoft.EntityFrameworkCore;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

// Kết quả truy vấn gom nhóm thô
internal sealed class StatSqlRow
{
    public string Metric { get; set; } = string.Empty;

    public string Dimension { get; set; } = string.Empty;

    // Cùng kiểu với DailyStats.Value
    [Precision(18, 2)]
    public decimal Value { get; set; }
}
