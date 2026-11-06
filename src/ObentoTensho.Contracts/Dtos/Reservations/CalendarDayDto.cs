namespace ObentoTensho.Contracts.Dtos.Reservations;

/// <summary>Lịch ngày cho màn hình lễ tân: mức đầy từng ca để tô màu xanh/vàng/đỏ (FR-RSV-05).</summary>
public sealed record CalendarDayDto(DateOnly Date, IReadOnlyList<CalendarSlotDto> Slots);
