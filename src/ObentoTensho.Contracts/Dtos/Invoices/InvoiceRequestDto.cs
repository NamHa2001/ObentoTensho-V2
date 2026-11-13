using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Invoices;

/// <summary>Yêu cầu xuất hóa đơn cho kế toán. Số CCCD và SĐT luôn che; xem đầy đủ qua "Hiện thông tin" (có audit) hoặc tệp Excel.</summary>
public sealed record InvoiceRequestDto(
    Guid Id,
    Guid OrderId,
    string OrderCode,
    OrderChannel Channel,
    decimal TotalAmount,
    DateTime OrderCreatedAtUtc,
    InvoiceBuyerType BuyerType,
    string BuyerName,
    string? MaskedNationalId,
    string? TaxCode,
    string? Address,
    string? Email,
    string? MaskedPhone,
    string? Note,
    InvoiceRequestStatus Status,
    string? InvoiceNumber,
    DateTime? IssuedAtUtc,
    string? CancelReason,
    DateTime CreatedAtUtc,
    string Version);
