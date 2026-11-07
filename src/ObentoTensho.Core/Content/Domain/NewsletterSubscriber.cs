namespace ObentoTensho.Core.Content.Domain;

// docs/srs/08 §7. IsActive chỉ bật sau khi xác nhận qua email (BR-CNT-21).
// Token hủy chỉ lưu hash để tra cứu và bản mã hóa để đưa vào từng thư; không lưu bản rõ
public sealed class NewsletterSubscriber
{
    public const int MaxPerEmailPerHour = 3;
    public static readonly TimeSpan ConfirmLifetime = TimeSpan.FromHours(48);

    public int Id { get; private set; }

    public required string Email { get; init; }

    public required string Lang { get; set; }

    public bool IsActive { get; set; }

    public DateTime? ConfirmedAtUtc { get; set; }

    public string? ConfirmTokenHash { get; set; }

    public DateTime? ConfirmExpiryUtc { get; set; }

    public required string UnsubscribeTokenHash { get; init; }

    public required byte[] UnsubscribeTokenProtected { get; init; }

    public required DateTime SubscribedAtUtc { get; set; }

    public DateTime? UnsubscribedAtUtc { get; set; }

    public bool IsSubscribed => IsActive && ConfirmedAtUtc is not null;
}
