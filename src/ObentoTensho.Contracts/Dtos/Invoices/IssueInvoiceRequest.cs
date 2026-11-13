namespace ObentoTensho.Contracts.Dtos.Invoices;

/// <summary>Đánh dấu đã xuất: số hóa đơn trên phần mềm hóa đơn điện tử (tối đa 50 ký tự).</summary>
public sealed record IssueInvoiceRequest(string InvoiceNumber);
