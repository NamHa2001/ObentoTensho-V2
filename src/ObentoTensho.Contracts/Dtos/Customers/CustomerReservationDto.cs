using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Customers;

public sealed record CustomerReservationDto(Guid Id, string ReservationCode, DateOnly ReservedDate, TimeOnly ReservedTime, ReservationStatus Status);
