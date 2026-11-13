namespace ObentoTensho.Contracts.Dtos.Invoices;

/// <summary>Hủy yêu cầu (khách đổi ý, nhập sai): lý do bắt buộc.</summary>
public sealed record CancelInvoiceRequest(string Reason);
