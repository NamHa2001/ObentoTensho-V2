namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Tra cứu bằng mã đơn và 4 số cuối SĐT (SEC-01 c).</summary>
public sealed record LookupOrderRequest(string Code, string PhoneLast4);
