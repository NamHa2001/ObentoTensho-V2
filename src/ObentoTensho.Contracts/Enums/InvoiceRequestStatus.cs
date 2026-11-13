namespace ObentoTensho.Contracts.Enums;

// Kế toán lập hóa đơn trên phần mềm hóa đơn điện tử rồi đánh dấu đã xuất (kèm số hóa đơn)
public enum InvoiceRequestStatus : byte
{
    Pending = 0,
    Issued = 1,
    Cancelled = 2
}
