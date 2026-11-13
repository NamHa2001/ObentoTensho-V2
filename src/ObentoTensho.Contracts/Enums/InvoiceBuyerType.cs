namespace ObentoTensho.Contracts.Enums;

// Người mua trên hóa đơn: cá nhân (họ tên, số CCCD) hay doanh nghiệp (tên công ty, mã số thuế, địa chỉ)
public enum InvoiceBuyerType : byte
{
    Individual = 0,
    Company = 1
}
