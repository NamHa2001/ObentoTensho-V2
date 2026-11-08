namespace ObentoTensho.Core.Notifications.Domain;

public enum RecipientLimit
{
    Within,

    // Thư quảng bá vượt giới hạn: dời sang ngày sau
    Defer,

    // Thư giao dịch vượt trần chống spam: bỏ
    Suppress
}
