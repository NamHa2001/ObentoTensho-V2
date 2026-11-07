namespace ObentoTensho.Contracts.Dtos.Vouchers;

/// <summary>
/// Sinh hàng loạt (BR-VCH-10): <c>Quantity</c> 1..5.000 mã cùng điều kiện <c>Template</c> (bỏ qua <c>Template.Code</c>);
/// <c>Prefix</c> tùy chọn (chữ/số, ≤ 10 ký tự) + 10 ký tự ngẫu nhiên. Trên 500 mã cần người thứ hai duyệt (BR-VCH-21).
/// </summary>
public sealed record BulkGenerateVouchersRequest(string? Prefix, int Quantity, VoucherRequest Template);
