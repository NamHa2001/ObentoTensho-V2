using ObentoTensho.Core.Notifications.Domain;

namespace ObentoTensho.Core.Notifications.Abstractions;

// Mẫu theo ngôn ngữ của người nhận; biến được escape trong HTML (SEC-59)
public interface IEmailTemplateRenderer
{
    EmailMessage Render(EmailRequest request);
}
