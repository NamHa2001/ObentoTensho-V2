namespace ObentoTensho.Contracts.Dtos.Reservations;

/// <summary>Tra cứu bằng mã đặt bàn và 4 số cuối SĐT (SEC-01 c).</summary>
public sealed record LookupReservationRequest(string Code, string PhoneLast4);
