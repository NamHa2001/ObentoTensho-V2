namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>
/// Đơn vừa tạo kèm token theo dõi (BR-ORD-11). Token chỉ trả một lần: giao diện giữ lại để theo dõi/hủy;
/// email xác nhận cũng có liên kết chứa token.
/// </summary>
public sealed record CreatedOrderDto(PublicOrderDto Order, string AccessToken);
