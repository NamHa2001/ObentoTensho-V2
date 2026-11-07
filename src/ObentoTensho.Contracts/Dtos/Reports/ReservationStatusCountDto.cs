using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Reports;

public sealed record ReservationStatusCountDto(ReservationStatus Status, int Count);
