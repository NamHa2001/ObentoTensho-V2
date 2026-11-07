namespace ObentoTensho.Contracts.Enums;

// docs/srs/08 §7: 0 chờ · 1 đã gửi · 2 lỗi
public enum NewsletterRecipientStatus : byte
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}
