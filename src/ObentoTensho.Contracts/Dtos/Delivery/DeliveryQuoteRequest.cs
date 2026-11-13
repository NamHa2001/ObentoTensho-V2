namespace ObentoTensho.Contracts.Dtos.Delivery;

/// <summary>Địa chỉ giao (số nhà, đường, phường, quận, thành phố) để tính quãng đường và phí ship.</summary>
public sealed record DeliveryQuoteRequest(string Address);
