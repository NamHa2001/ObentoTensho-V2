using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Core.Content.Abstractions;

// docs/srs/20 #18, #19: ghi vào hộp thư đi cùng transaction, lỗi SMTP không làm request lỗi (BR-CNT-33)
public interface IContactEmailOutbox
{
    void EnqueueReceived(ContactMessage message, string language);

    void EnqueueInternal(ContactMessage message, string staffEmail);
}
