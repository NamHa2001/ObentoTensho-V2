using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Core.Tables.Services;

// Ánh xạ thực thể → DTO (AR-08: thực thể không ra khỏi Service)
public static class TableMappings
{
    public static TableAreaDto ToDto(this TableArea area) =>
        new(area.Id, area.Name, area.IsActive, RowVersions.Encode(area.RowVersion));

    public static TableDto ToDto(this DiningTable table) =>
        new(
            table.Id,
            table.AreaId,
            table.Name,
            table.MinCapacity,
            table.MaxCapacity,
            table.CurrentStatus,
            new TableLayout(table.PosX, table.PosY, table.Width, table.Height, table.Rotation),
            table.IsActive,
            RowVersions.Encode(table.RowVersion));

    public static ServiceSlotDto ToDto(this ServiceSlot slot) =>
        new(slot.Id, slot.DayOfWeek, slot.StartTime, slot.EndTime, slot.MaxReservations, slot.MaxGuests, slot.IsActive, RowVersions.Encode(slot.RowVersion));

    public static BlockedDateDto ToDto(this BlockedDate blocked) =>
        new(blocked.Id, blocked.Date, blocked.AppliesTo, blocked.StartTime, blocked.EndTime, blocked.Reason);

    public static IReadOnlyList<OpeningDayDto> ToWeeklyDto(this IEnumerable<OpeningHour> hours)
    {
        var byDay = hours.ToLookup(h => h.DayOfWeek);
        return Enumerable.Range(0, 7)
            .Select(day =>
            {
                var open = byDay[(byte)day].Where(h => !h.IsClosed).OrderBy(h => h.OpenTime).ToArray();
                return new OpeningDayDto(
                    day,
                    open.Length == 0,
                    open.Select(h => new OpeningRangeDto(h.OpenTime, h.CloseTime, h.CrossesMidnight)).ToArray());
            })
            .ToArray();
    }

    public static IReadOnlyList<SpecialHoursDto> ToSpecialDto(this IEnumerable<SpecialHour> hours) =>
        hours
            .GroupBy(h => h.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var open = g.Where(h => !h.IsClosed && h.OpenTime is not null && h.CloseTime is not null).OrderBy(h => h.OpenTime).ToArray();
                return new SpecialHoursDto(
                    g.Key,
                    open.Length == 0,
                    open.Select(h => new OpeningRangeDto(h.OpenTime.GetValueOrDefault(), h.CloseTime.GetValueOrDefault(), false)).ToArray(),
                    g.Select(h => h.Note).FirstOrDefault(n => n is not null));
            })
            .ToArray();
}
