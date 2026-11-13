using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Invoices.Abstractions;

// Lọc theo ngày tạo yêu cầu (giờ VN); Q tìm theo mã đơn, tên người mua, MST
public sealed record InvoiceQuery(InvoiceRequestStatus? Status, DateOnly? From, DateOnly? To, string? Q, int Page, int PageSize);
