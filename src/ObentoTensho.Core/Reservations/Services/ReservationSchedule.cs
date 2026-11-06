using ObentoTensho.Contracts.Dtos.Reservations;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Tables.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Reservations.Services;

// BR-RSV-01/02: ngày trong khoảng nhận đặt, giờ thuộc ca đang bật, ca nằm trong giờ mở cửa của ngày đó
// (kể cả giờ đặc biệt), không bị khóa. Mọi so sánh theo giờ VN (BR-TBL-01).
public sealed class ReservationSchedule(
    IScheduleRepository schedule,
    IReservationRepository reservations,
    IReservationSettings settings,
    IClock clock)
{
    public async Task<AvailabilityDto> GetAvailabilityAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var today = clock.TodayLocal;
        if (date < today || date > today.AddDays(settings.MaxAdvanceDays))
        {
            return new AvailabilityDto(date, false, []);
        }

        var day = await LoadDayAsync(date, cancellationToken);
        var capacities = (await reservations.GetCapacitiesAsync(date, date, cancellationToken)).ToDictionary(c => c.SlotId);
        var nowLocal = clock.ToLocal(clock.UtcNow);

        var slots = day.Slots
            .Where(s => day.Schedule.IsOpen(date, s.StartTime, s.EndTime))
            .Select(s =>
            {
                var booked = capacities.GetValueOrDefault(s.Id);
                var times = Times(s)
                    .Where(t => date.ToDateTime(t) > nowLocal && !day.IsBlocked(date, t))
                    .ToArray();
                return new SlotAvailabilityDto(
                    s.Id,
                    s.StartTime,
                    s.EndTime,
                    Math.Max(0, s.MaxReservations - (booked?.BookedCount ?? 0)),
                    s.MaxGuests is { } max ? Math.Max(0, max - (booked?.BookedGuests ?? 0)) : null,
                    times);
            })
            .Where(s => s.Times.Count > 0)
            .ToArray();

        return new AvailabilityDto(date, slots.Length > 0, slots);
    }

    // Trả ca chứa giờ đặt. Lễ tân (staff) được đặt cho giờ đã qua trong hôm nay (khách đến trực tiếp).
    public async Task<Result<ServiceSlot>> CheckBookableAsync(DateOnly date, TimeOnly time, bool staff, CancellationToken cancellationToken)
    {
        var today = clock.TodayLocal;
        var inPast = date < today || (!staff && date.ToDateTime(time) <= clock.ToLocal(clock.UtcNow));
        if (inPast || date > today.AddDays(settings.MaxAdvanceDays))
        {
            return Result.Failure<ServiceSlot>(ErrorCodes.DateOutOfRange);
        }

        var day = await LoadDayAsync(date, cancellationToken);
        var slot = day.Slots.FirstOrDefault(s => time >= s.StartTime && time < s.EndTime);
        if (slot is null)
        {
            return Result.Failure<ServiceSlot>(ErrorCodes.OutsideServiceSlot);
        }

        if (!day.Schedule.IsOpen(date, slot.StartTime, slot.EndTime))
        {
            return Result.Failure<ServiceSlot>(ErrorCodes.OutsideOpeningHours);
        }

        return day.IsBlocked(date, time) ? Result.Failure<ServiceSlot>(ErrorCodes.DateBlocked) : Result.Success(slot);
    }

    private async Task<DaySchedule> LoadDayAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var weekly = await schedule.ListWeeklyAsync(forUpdate: false, cancellationToken);
        var special = await schedule.ListSpecialAsync(date.AddDays(-1), date, forUpdate: false, cancellationToken);
        var blocked = (await schedule.ListBlockedAsync(date, cancellationToken)).Where(b => b.Date == date).ToArray();
        var dayOfWeek = (byte)date.DayOfWeek;
        var slots = (await schedule.ListSlotsAsync(cancellationToken))
            .Where(s => s.IsActive && s.AppliesTo(dayOfWeek))
            .OrderBy(s => s.StartTime)
            .ToArray();
        return new DaySchedule(new OpeningSchedule(weekly, special), slots, blocked);
    }

    private IEnumerable<TimeOnly> Times(ServiceSlot slot)
    {
        var step = TimeSpan.FromMinutes(settings.TimeStepMinutes);
        for (var t = slot.StartTime; t < slot.EndTime && t >= slot.StartTime; t = t.Add(step))
        {
            yield return t;
        }
    }

    private sealed record DaySchedule(OpeningSchedule Schedule, IReadOnlyList<ServiceSlot> Slots, IReadOnlyList<BlockedDate> Blocks)
    {
        public bool IsBlocked(DateOnly date, TimeOnly time) => Blocks.Any(b => b.Covers(date, time, BlockScope.Reservation));
    }
}
