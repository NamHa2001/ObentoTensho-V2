using ObentoTensho.Core.Reports.Domain;

namespace ObentoTensho.Core.Reports.Abstractions;

public interface IStatsRepository
{
    // Tính một ngày từ bảng gốc bằng một truy vấn gom nhóm trong SQL (BR-RPT-10); [startUtc, endUtc)
    Task<IReadOnlyList<StatRow>> ComputeDayAsync(DateOnly day, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken);

    // Xóa dòng cũ, thêm dòng mới vào unit of work; bên gọi lưu trong transaction
    Task ReplaceDayAsync(DateOnly day, IReadOnlyList<StatRow> rows, DateTime nowUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<DailyStat>> ReadAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<DashboardExtras> LoadDashboardExtrasAsync(DateOnly today, DateTime nowUtc, CancellationToken cancellationToken);
}
