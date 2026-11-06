namespace ObentoTensho.Contracts.Dtos.Reservations;

/// <summary>Văn bản thuần (BR-RSV-26): ghi chú cuộc gọi, ghi chú nội bộ.</summary>
public sealed record ReservationNoteRequest(string? Note);
