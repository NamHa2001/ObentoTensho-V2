using MimeKit;
using ObentoTensho.Core.Notifications.Domain;

namespace ObentoTensho.Infrastructure.Email;

// BR-NTF-10: From = no-reply của tên miền gửi thư, Reply-To = email quán; luôn có bản text thuần
internal static class MimeMessageFactory
{
    public static MimeMessage Create(EmailMessage message, EmailOptions options)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        if (!string.IsNullOrWhiteSpace(options.ReplyTo))
        {
            mime.ReplyTo.Add(MailboxAddress.Parse(options.ReplyTo));
        }

        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();
        return mime;
    }
}
