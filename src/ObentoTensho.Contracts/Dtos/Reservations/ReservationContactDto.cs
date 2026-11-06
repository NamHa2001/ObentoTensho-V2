namespace ObentoTensho.Contracts.Dtos.Reservations;

/// <summary>Liên hệ đầy đủ — chỉ trả khi lễ tân bấm "Hiện liên hệ" để gọi xác minh.</summary>
public sealed record ReservationContactDto(string Phone, string? Email);
