using ObentoTensho.Core.Reservations.Domain;

namespace ObentoTensho.Core.Reservations.Abstractions;

// Thư cho khách về đặt bàn (docs/srs/20 #2, #4, #5, #8), ghi vào hộp thư đi cùng transaction; bỏ qua nếu khách không có email
public interface IReservationEmailOutbox
{
    void EnqueueReceived(Reservation reservation, string accessToken);

    // accessToken: chỉ có khi lễ tân vừa tạo đặt bàn (chưa gửi thư "đã nhận" chứa liên kết hủy)
    void EnqueueConfirmed(Reservation reservation, string? accessToken);

    void EnqueueRejected(Reservation reservation, string reason);

    void EnqueueCancelled(Reservation reservation);

    // BR-RSV-08: nhắc một lần trước giờ hẹn
    void EnqueueReminder(Reservation reservation);

    // BR-RSV-25: quán chưa kịp xác nhận nên hệ thống tự hủy — xin lỗi, mời chọn giờ khác
    void EnqueueAutoCancelled(Reservation reservation);
}
