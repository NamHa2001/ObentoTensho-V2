using ObentoTensho.Core.Orders.Domain;

namespace ObentoTensho.Core.Orders.Abstractions;

// Núm vặn `order.*` (docs/srs/19 §3)
public interface IOrderSettings
{
    // BR-ORD-05: giờ lấy sớm nhất sau bây giờ, và phải trước giờ đóng cửa bấy nhiêu phút
    int MinLeadMinutes { get; }

    int MaxLeadDays { get; }

    // Khoảng cách giữa các giờ lấy gợi ý cho khách
    int PickupStepMinutes { get; }

    // BR-ORD-20
    int MaxActivePerPhone { get; }

    // BR-ORD-21: đơn tiền mặt từ mức này phải gọi xác nhận
    decimal CallConfirmThreshold { get; }

    // BR-ORD-01
    int MaxLines { get; }

    // BR-ORD-18: sẵn sàng quá bấy nhiêu phút sau giờ lấy ⇒ báo "không đến lấy"
    int NoPickupMinutes { get; }

    // BR-ORD-18: số lần không đến lấy trong 90 ngày để SĐT bị gắn cờ
    int NoPickupFlagCount { get; }

    // docs/srs/20 #30: chờ xác nhận quá bấy nhiêu phút (hoặc còn ít hơn chừng ấy tới giờ lấy) ⇒ cần xử lý
    int PendingAlertMinutes { get; }

    // docs/srs/23 BR-PLT-03/04: khấu trừ, GTGT, TNCN (%) trên doanh thu ròng, chụp vào đơn lúc hoàn thành
    RevenueSplit.Rates RevenueRates { get; }
}
