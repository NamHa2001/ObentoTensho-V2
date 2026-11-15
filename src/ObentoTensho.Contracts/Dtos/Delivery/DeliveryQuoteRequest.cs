namespace ObentoTensho.Contracts.Dtos.Delivery;

/// <summary>
/// Địa chỉ giao (số nhà, đường, phường, quận, thành phố) để tính quãng đường và phí ship.
/// <c>Latitude</c>/<c>Longitude</c>: điểm khách ghim trên bản đồ (có thì tính tới đúng điểm này thay vì tìm theo địa chỉ).
/// </summary>
public sealed record DeliveryQuoteRequest(string Address, double? Latitude = null, double? Longitude = null);
