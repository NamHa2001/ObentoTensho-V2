using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Invoices.Domain;

namespace ObentoTensho.Core.Invoices.Abstractions;

// Yêu cầu kèm thông tin đơn cần cho kế toán
public sealed record InvoiceRow(InvoiceRequest Request, string OrderCode, OrderChannel Channel, decimal TotalAmount, DateTime OrderCreatedAtUtc);
