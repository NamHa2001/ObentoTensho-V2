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
        foreach (var (name, value) in message.Headers ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            mime.Headers.Add(name, value);
        }

        var body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody };
        foreach (var attachment in message.Attachments ?? [])
        {
            body.Attachments.Add(attachment.FileName, System.Text.Encoding.UTF8.GetBytes(attachment.Content), ContentType.Parse(attachment.ContentType));
        }

        mime.Body = body.ToMessageBody();
        return mime;
    }
}
