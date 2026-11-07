using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Realtime;

public sealed record ReservationEventDto(Guid Id, string Code, ReservationStatus Status, int SlotId);
