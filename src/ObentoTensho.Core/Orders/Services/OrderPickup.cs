using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Tables.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Orders.Services;

// BR-ORD-05: giờ lấy (giờ VN) ≥ bây giờ + minLead, trong maxLeadDays ngày, nằm trong giờ mở cửa của ngày đó
// và trước giờ đóng ít nhất minLead phút; không trùng khung khóa nhận đơn (BR-TBL-18)
public sealed class OrderPickup(IScheduleRepository schedule, IOrderSettings settings, IClock clock)
{
    public async Task<Result<DateTime>> CheckAsync(DateOnly date, TimeOnly time, CancellationToken cancellationToken)
    {
        var local = date.ToDateTime(time);
        if (local < Earliest() || date > clock.TodayLocal.AddDays(settings.MaxLeadDays))
        {
            return Result.Failure<DateTime>(ErrorCodes.DateOutOfRange);
        }

        var day = await LoadAsync(date, cancellationToken);
        if (!day.Intervals.Any(i => local >= i.Start && local <= LastPickup(i)))
        {
            return Result.Failure<DateTime>(ErrorCodes.OutsideOpeningHours);
        }

        return day.IsBlocked(date, time) ? Result.Failure<DateTime>(ErrorCodes.DateBlocked) : Result.Success(clock.ToUtc(date, time));
    }

    // Các mốc cách đều pickupStepMinutes để khách chọn (vẫn nhận giờ lẻ hợp lệ khi gửi thẳng)
    public async Task<PickupTimesDto> TimesAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var today = clock.TodayLocal;
        if (date < today || date > today.AddDays(settings.MaxLeadDays))
        {
            return new PickupTimesDto(date, []);
        }

        var day = await LoadAsync(date, cancellationToken);
        var earliest = Earliest();
        var step = TimeSpan.FromMinutes(settings.PickupStepMinutes);
        var times = new SortedSet<TimeOnly>();
        foreach (var interval in day.Intervals)
        {
            var last = LastPickup(interval);
            for (var t = RoundUp(interval.Start > earliest ? interval.Start : earliest, step); t <= last; t += step)
            {
                var time = TimeOnly.FromDateTime(t);
                if (DateOnly.FromDateTime(t) == date && !day.IsBlocked(date, time))
                {
                    times.Add(time);
                }
            }
        }

        return new PickupTimesDto(date, times.ToArray());
    }

    private DateTime Earliest() => clock.ToLocal(clock.UtcNow).AddMinutes(settings.MinLeadMinutes);

    private DateTime LastPickup(OpenInterval interval) => interval.End.AddMinutes(-settings.MinLeadMinutes);

    private static DateTime RoundUp(DateTime value, TimeSpan step) =>
        new((value.Ticks + step.Ticks - 1) / step.Ticks * step.Ticks, value.Kind);

    private async Task<Day> LoadAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var weekly = await schedule.ListWeeklyAsync(forUpdate: false, cancellationToken);
        var special = await schedule.ListSpecialAsync(date.AddDays(-1), date, forUpdate: false, cancellationToken);
        var blocked = (await schedule.ListBlockedAsync(date, cancellationToken)).Where(b => b.Date == date).ToArray();
        return new Day(new OpeningSchedule(weekly, special).IntervalsCovering(date), blocked);
    }

    private sealed record Day(IReadOnlyList<OpenInterval> Intervals, IReadOnlyList<BlockedDate> Blocks)
    {
        public bool IsBlocked(DateOnly date, TimeOnly time) => Blocks.Any(b => b.Covers(date, time, BlockScope.Order));
    }
}
