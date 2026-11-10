namespace ObentoTensho.Core.Loyalty.Abstractions;

// Núm vặn `loyalty.*` (docs/srs/19 §3, D-34)
public interface ILoyaltySettings
{
    // BR-LOY-01: số đồng cho 1 điểm (≥ 1)
    int VndPerPoint { get; }

    // BR-AUTH-17: gắn đơn vãng lai đã hoàn thành thì cộng điểm hồi tố

    // BR-LOY-22: điểm mỗi lần check-in đặt bàn và trần mỗi tháng (giờ VN)
    int ReservationPoints { get; }

    int ReservationPointsPerMonth { get; }

    // BR-LOY-06
    int PointExpiryMonths { get; }

    // BR-LOY-12: báo trước khi hết hạn bấy nhiêu ngày
    int ExpiryNoticeDays { get; }

    // BR-LOY-21
    int AdjustApprovalThreshold { get; }

    // BR-LOY-09: giá trị voucher sinh nhật (₫); 0 = tắt
    decimal BirthdayVoucherValue { get; }

    // BR-LOY-03: ngưỡng điểm tích lũy và % giảm của từng hạng
    int SilverPoints { get; }

    int GoldPoints { get; }

    int DiamondPoints { get; }

    int SilverDiscountPercent { get; }

    int GoldDiscountPercent { get; }

    int DiamondDiscountPercent { get; }
}
