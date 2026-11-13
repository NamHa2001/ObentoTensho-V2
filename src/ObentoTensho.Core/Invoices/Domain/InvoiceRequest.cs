using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Invoices.Domain;

// Yêu cầu xuất hóa đơn cho một đơn (chủ quán 2026-11-13). Phần mềm không tự phát hành hóa đơn điện tử:
// kế toán lập trên phần mềm của nhà cung cấp rồi ghi lại số hóa đơn. Mỗi đơn tối đa một yêu cầu còn hiệu lực.
public sealed class InvoiceRequest
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required Guid OrderId { get; init; }

    public required InvoiceBuyerType BuyerType { get; init; }

    // Cá nhân: họ tên; doanh nghiệp: tên công ty
    public required string BuyerName { get; init; }

    // Cá nhân: số CCCD 12 số
    public string? NationalId { get; init; }

    // Doanh nghiệp: mã số thuế
    public string? TaxCode { get; init; }

    public string? Address { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? Note { get; init; }

    public InvoiceRequestStatus Status { get; set; } = InvoiceRequestStatus.Pending;

    public string? InvoiceNumber { get; set; }

    public DateTime? IssuedAtUtc { get; set; }

    public Guid? IssuedByStaffId { get; set; }

    public string? CancelReason { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    // Null khi khách tự gửi lúc đặt món online
    public Guid? CreatedByStaffId { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public byte[] RowVersion { get; private set; } = [];
}
