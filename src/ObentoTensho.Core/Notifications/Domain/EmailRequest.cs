namespace ObentoTensho.Core.Notifications.Domain;

// Một email cần gửi, lưu trong hộp thư đi. Values là biến của mẫu (đã gồm liên kết có token nếu cần).
// IdempotencyKey: cùng khóa không bao giờ gửi hai lần (BR-NTF-03). Headers: header riêng của thư (BR-CNT-26).
public sealed record EmailRequest(
    EmailKind Kind,
    string To,
    string Language,
    IReadOnlyDictionary<string, string> Values,
    string IdempotencyKey,
    string? RelatedEntityId,
    IReadOnlyList<EmailAttachment>? Attachments = null,
    IReadOnlyDictionary<string, string>? Headers = null)
{
    // Biến duy nhất không escape trong HTML: nội dung bản tin đã làm sạch khi lưu (BR-CNT-24)
    public const string TrustedHtmlValue = "contentHtml";
}
