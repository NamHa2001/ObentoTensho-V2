namespace ObentoTensho.Core.Orders.Abstractions;

// Núm vặn `order.*` (docs/srs/19 §3); khi có bảng cài đặt sẽ đọc từ đó thay cho cấu hình
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
}
