namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Theo dõi bằng token trong thân request, không đặt token lên URL (SEC-46).</summary>
public sealed record TrackOrderRequest(string Code, string Token);
