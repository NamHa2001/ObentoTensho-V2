namespace ObentoTensho.Contracts.Dtos.Delivery;

/// <summary>Quãng đường lái xe (km, làm tròn 0,1) và phí ship server tính; giá trị này là căn cứ khi tạo đơn giao hàng.</summary>
public sealed record DeliveryQuoteDto(string Address, decimal DistanceKm, decimal Fee, int FreeKm, int FeePerKm);
