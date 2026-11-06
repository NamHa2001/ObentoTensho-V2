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
}
