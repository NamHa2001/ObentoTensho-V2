using System.Globalization;
using ObentoTensho.Contracts.Dtos.Reports;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Reports.Abstractions;
using ObentoTensho.Core.Reports.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Reports.Services;

// BR-RPT-05: DailyStats luôn tính lại nguyên ngày từ bảng gốc nên chạy lại không cộng dồn; đối soát phát hiện ngày đã chốt bị lệch
public sealed class StatsRollupService(
    IStatsRepository stats,
    BusinessCalendar calendar,
    IStaffRepository staff,
    IAdminEmailOutbox adminEmails,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public const int ReconcileDays = 7;
    public const int MaxRecomputeDays = 92;

    public async Task RefreshDayAsync(DateOnly day, CancellationToken cancellationToken)
    {
        var (start, end) = calendar.Bounds(day);
        await ReplaceAsync(day, await stats.ComputeDayAsync(day, start, end, cancellationToken), cancellationToken);
    }

    // PERF-55: nhập dữ liệu cũ thì tính lại từng ngày từ bảng gốc; tối đa 92 ngày một lần, không quá hôm nay, có audit
    public async Task<Result<RecomputeStatsDto>> RecomputeAsync(RecomputeStatsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ToDate < request.FromDate || request.ToDate > calendar.Today || request.ToDate.DayNumber - request.FromDate.DayNumber >= MaxRecomputeDays)
        {
            return Result.Failure<RecomputeStatsDto>(ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["toDate"] = [FieldErrorCodes.OutOfRange],
            });
        }

        var days = 0;
        for (var day = request.FromDate; day <= request.ToDate; day = day.AddDays(1))
        {
            await RefreshDayAsync(day, cancellationToken);
            days++;
        }

        audit.Record(new AuditEntry("reports.daily_stats.recomputed", "daily_stats", $"{request.FromDate:yyyy-MM-dd}..{request.ToDate:yyyy-MM-dd}", After: new { days }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new RecomputeStatsDto(days));
    }

    public async Task<int> RefreshRecentAsync(CancellationToken cancellationToken)
    {
        var today = calendar.Today;
        await RefreshDayAsync(today, cancellationToken);
        await RefreshDayAsync(today.AddDays(-1), cancellationToken);
        return 2;
    }

    // Chỉ so dòng tổng: dòng theo trạng thái thay đổi hợp lệ khi đơn đổi trạng thái sau đó
    public async Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        var today = calendar.Today;
        var mismatched = new List<DateOnly>();
        for (var offset = 1; offset <= ReconcileDays; offset++)
        {
            var day = today.AddDays(-offset);
            var (start, end) = calendar.Bounds(day);
            var fresh = await stats.ComputeDayAsync(day, start, end, cancellationToken);
            var stored = await stats.ReadAsync(day, day, cancellationToken);
            if (!SameTotals(fresh, stored))
            {
                mismatched.Add(day);
                await ReplaceAsync(day, fresh, cancellationToken);
            }
        }

        if (mismatched.Count > 0)
        {
            await AlertAsync(mismatched, cancellationToken);
        }

        return mismatched.Count;
    }

    private Task<bool> ReplaceAsync(DateOnly day, IReadOnlyList<StatRow> rows, CancellationToken cancellationToken) =>
        unitOfWork.InTransactionAsync(
            async ct =>
            {
                await stats.ReplaceDayAsync(day, rows, clock.UtcNow, ct);
                await unitOfWork.SaveChangesAsync(ct);
                return true;
            },
            cancellationToken);

    private static bool SameTotals(IReadOnlyList<StatRow> fresh, IReadOnlyList<DailyStat> stored)
    {
        var expected = fresh.Where(r => r.Dimension.Length == 0).ToDictionary(r => r.Metric, r => r.Value, StringComparer.Ordinal);
        var actual = stored.Where(r => r.Dimension.Length == 0).ToDictionary(r => r.Metric, r => r.Value, StringComparer.Ordinal);
        return expected.Count == actual.Count && expected.All(e => actual.TryGetValue(e.Key, out var value) && value == e.Value);
    }

    private async Task AlertAsync(List<DateOnly> days, CancellationToken cancellationToken)
    {
        var list = string.Join(", ", days.Select(d => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
        var key = $"stats:{clock.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)}";
        foreach (var email in await staff.ListActiveEmailsAsync([Contracts.Enums.StaffRole.SuperAdmin], cancellationToken))
        {
            adminEmails.EnqueueAlert(email, $"Đối soát số liệu lệch ở các ngày: {list}. Đã tính lại từ dữ liệu gốc.", key);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
