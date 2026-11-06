namespace ObentoTensho.Core.Notifications.Domain;

public enum DispatchOutcome
{
    // Đã gửi (lần này hoặc trước đó với cùng khóa)
    Sent,

    // Không gửi vì vượt giới hạn theo địa chỉ nhận — không thử lại
    Suppressed,

    // Hết hạn mức ngày cho nhóm ưu tiên này — thử lại vào ngày mai
    Deferred,

    // Lỗi tạm thời (SMTP…) — hộp thư đi thử lại sau
    RetryLater
}
