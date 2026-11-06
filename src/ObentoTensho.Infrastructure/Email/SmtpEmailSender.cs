using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Notifications.Abstractions;
using ObentoTensho.Core.Notifications.Domain;

namespace ObentoTensho.Infrastructure.Email;

// Gửi qua SMTP relay của nhà cung cấp (Brevo/Mailgun/SES…). Lỗi ném ra để hộp thư đi thử lại.
internal sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private const int TimeoutMilliseconds = 30_000;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var client = new SmtpClient { Timeout = TimeoutMilliseconds };

        // Cổng 465 dùng TLS ngay khi kết nối; cổng khác bắt buộc nâng cấp STARTTLS — không gửi mật khẩu qua kênh rõ
        var security = settings.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        // Đã kiểm khi khởi động (EmailOptions.Validate); ném lỗi rõ ràng nếu cấu hình bị đổi lúc chạy
        await client.ConnectAsync(settings.SmtpHost ?? throw new InvalidOperationException("Thiếu Email:SmtpHost."), settings.SmtpPort, security, cancellationToken);
        await client.AuthenticateAsync(
            settings.SmtpUsername ?? throw new InvalidOperationException("Thiếu Email:SmtpUsername."),
            settings.SmtpPassword ?? throw new InvalidOperationException("Thiếu Email:SmtpPassword."),
            cancellationToken);
        await client.SendAsync(MimeMessageFactory.Create(message, settings), cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
