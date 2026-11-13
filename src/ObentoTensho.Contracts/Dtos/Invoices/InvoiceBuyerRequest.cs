using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Invoices;

/// <summary>
/// Thông tin người mua để xuất hóa đơn. Cá nhân: <c>BuyerName</c> (họ tên) + <c>NationalId</c> (CCCD 12 số), SĐT/email tùy chọn.
/// Doanh nghiệp: <c>BuyerName</c> (tên công ty) + <c>TaxCode</c> (MST 10 số, hoặc 10-3 / 13 số cho chi nhánh) + <c>Address</c> + <c>Email</c> nhận hóa đơn.
/// </summary>
public sealed record InvoiceBuyerRequest(
    InvoiceBuyerType BuyerType,
    string BuyerName,
    string? NationalId,
    string? TaxCode,
    string? Address,
    string? Email,
    string? Phone,
    string? Note);
