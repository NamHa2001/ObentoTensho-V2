namespace ObentoTensho.Infrastructure.Loyalty;

// Thông số kỹ thuật ngoài danh mục cài đặt
public sealed class LoyaltyOptions
{
    public const string SectionName = "Loyalty";

    public int ExpiryNoticeDays { get; set; } = 30;

    public bool IsValid() => ExpiryNoticeDays is >= 1 and <= 90;
}
