namespace ObentoTensho.Core.Vouchers.Abstractions;

// Núm vặn `voucher.*` (docs/srs/19 §3)
public interface IVoucherSettings
{
    // BR-LOY-07: voucher đổi điểm còn hạn bấy nhiêu ngày
    int RedeemExpiryDays { get; }

    // BR-VCH-21
    int ApprovalPercent { get; }

    decimal ApprovalAmount { get; }

    int BulkApprovalQuantity { get; }
}
