using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Vouchers;

/// <summary>
/// Mã hợp lệ cho giỏ hiện tại. Voucher và ưu đãi hạng loại trừ nhau (BR-VCH-04): <c>MemberDiscount</c> là mức giảm theo hạng
/// nếu không dùng voucher; <c>MemberIsBetter</c> ⇒ giao diện hỏi khách có muốn dùng voucher không (BR-VCH-19).
/// </summary>
public sealed record VoucherCheckDto(
    string Code,
    decimal SubTotal,
    decimal VoucherDiscount,
    decimal TotalWithVoucher,
    decimal MemberDiscount,
    decimal TotalWithMemberDiscount,
    bool MemberIsBetter,
    decimal ShippingDiscount = 0,
    VoucherTarget AppliesTo = VoucherTarget.Items);
