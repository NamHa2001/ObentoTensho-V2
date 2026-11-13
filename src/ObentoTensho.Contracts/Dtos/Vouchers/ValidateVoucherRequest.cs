using ObentoTensho.Contracts.Dtos.Orders;

namespace ObentoTensho.Contracts.Dtos.Vouchers;

/// <summary>Kiểm mã ở giỏ (BR-VCH-02). Tạm tính do server tính lại từ giỏ; khách lấy từ phiên đăng nhập, không nhận từ thân request (BR-VCH-09).</summary>
public sealed record ValidateVoucherRequest(string Code, IReadOnlyList<OrderLineRequest> Items, decimal? DeliveryFee = null);
