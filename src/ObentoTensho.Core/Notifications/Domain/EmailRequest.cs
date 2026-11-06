namespace ObentoTensho.Core.Notifications.Domain;

// Một email cần gửi, lưu trong hộp thư đi. Values là biến của mẫu (đã gồm liên kết có token nếu cần).
// IdempotencyKey: cùng khóa không bao giờ gửi hai lần (BR-NTF-03).
public sealed record EmailRequest(
    EmailKind Kind,
    string To,
    string Language,
    IReadOnlyDictionary<string, string> Values,
    string IdempotencyKey,
    string? RelatedEntityId);
