using ObentoTensho.Core.Vouchers.Abstractions;

namespace ObentoTensho.Infrastructure.Vouchers;

// Giá trị mặc định theo docs/srs/19 §3 (`voucher.*`)
public sealed class VoucherOptions : IVoucherSettings
{
    public const string SectionName = "Voucher";

    public int RedeemExpiryDays { get; set; } = 30;

    public int ApprovalPercent { get; set; } = 50;

    public decimal ApprovalAmount { get; set; } = 200_000m;

    public int BulkApprovalQuantity { get; set; } = 500;

    public bool IsValid() =>
        RedeemExpiryDays is >= 1 and <= 365 && ApprovalPercent is >= 1 and <= 100 && ApprovalAmount >= 1 && BulkApprovalQuantity is >= 1 and <= 5000;
}
