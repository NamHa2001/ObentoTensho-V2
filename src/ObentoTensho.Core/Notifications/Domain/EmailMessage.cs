namespace ObentoTensho.Core.Notifications.Domain;

// Thư đã dựng từ mẫu, sẵn sàng giao cho nhà cung cấp gửi
public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody, IReadOnlyList<EmailAttachment>? Attachments = null);
