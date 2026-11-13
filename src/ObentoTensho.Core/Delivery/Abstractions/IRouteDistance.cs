namespace ObentoTensho.Core.Delivery.Abstractions;

// Quãng đường lái xe thật (không phải đường chim bay) giữa hai địa chỉ hoặc "vĩ độ,kinh độ"
public interface IRouteDistance
{
    Task<RouteDistanceResult> DrivingAsync(string origin, string destination, CancellationToken cancellationToken);
}
