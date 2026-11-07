namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Khách tự hủy khi đơn còn chờ xác nhận (BR-ORD-17): token theo dõi, hoặc đã đăng nhập đúng chủ đơn.</summary>
public sealed record CancelOrderRequest(string? Token, string? Reason);
