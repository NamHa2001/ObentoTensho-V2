namespace ObentoTensho.Infrastructure.Vouchers;

// Thông số kỹ thuật ngoài danh mục cài đặt
public sealed class VoucherOptions
{
    public const string SectionName = "Voucher";

    public int BulkApprovalQuantity { get; set; } = 500;

    public bool IsValid() => BulkApprovalQuantity >= 1;
}
