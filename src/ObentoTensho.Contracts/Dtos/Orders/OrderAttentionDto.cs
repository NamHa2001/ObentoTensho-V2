namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>
/// Đơn "cần xử lý" (docs/srs/20 #30): chờ xác nhận quá lâu hoặc sắp tới giờ lấy, sẵn sàng mà khách chưa đến lấy (BR-ORD-18),
/// đã thu tiền rồi hủy mà chưa ghi hoàn tiền (BR-ORD-16).
/// </summary>
public sealed record OrderAttentionDto(
    IReadOnlyList<AdminOrderDto> PendingOverdue,
    IReadOnlyList<AdminOrderDto> NotPickedUp,
    IReadOnlyList<AdminOrderDto> RefundPending);
