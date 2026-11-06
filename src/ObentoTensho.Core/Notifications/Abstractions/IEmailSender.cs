using ObentoTensho.Core.Notifications.Domain;

namespace ObentoTensho.Core.Notifications.Abstractions;

// BR-NTF-11: không phụ thuộc nhà cung cấp; lỗi tạm thời ném ngoại lệ để hộp thư đi thử lại
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
