namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>
/// Kết quả kiểm một dòng giỏ. <c>ErrorCode</c> khác null ⇒ dòng không đặt được: ITEM_UNAVAILABLE (món hết/ẩn/xóa/không bán mang về),
/// OPTION_INVALID / OPTION_UNAVAILABLE (tùy chọn sai hoặc vừa hết), VALIDATION_FAILED (số lượng, ghi chú).
/// </summary>
public sealed record QuoteLineDto(int Index, OrderLineDto? Line, string? ErrorCode);
