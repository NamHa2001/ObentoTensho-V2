using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Reservations;

/// <summary>Thông tin trả cho khách (SEC-01): SĐT/email đã che, không có ghi chú nội bộ.</summary>
public sealed record PublicReservationDto(
    string Code,
    DateOnly Date,
    TimeOnly Time,
    int AdultCount,
    int ChildCount,
    ReservationStatus Status,
    string GuestName,
    string MaskedPhone,
    string MaskedEmail,
    string? SpecialRequest);
