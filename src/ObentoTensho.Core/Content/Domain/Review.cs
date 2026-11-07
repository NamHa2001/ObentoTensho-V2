using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Core.Content.Domain;

// docs/srs/08 Reviews. Một đánh giá cho mỗi (User, Order, MenuItem); Comment/ReplyContent là văn bản thuần (BR-CNT-15)
public sealed class Review
{
    public const int WindowDays = 30;
    public const int MaxPerDay = 10;
    public static readonly TimeSpan EditWindow = TimeSpan.FromHours(24);

    public int Id { get; private set; }

    public required Guid UserId { get; init; }

    public required Guid OrderId { get; init; }

    public int? MenuItemId { get; init; }

    public required byte Rating { get; set; }

    public string? Comment { get; set; }

    public string? ReplyContent { get; set; }

    public DateTime? RepliedAtUtc { get; set; }

    public Guid? RepliedByStaffId { get; set; }

    public ReviewStatus Status { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public Guid? ReviewedByStaffId { get; set; }

    public required DateTime CreatedAtUtc { get; init; }

    public byte[] RowVersion { get; private set; } = [];

    // BR-CNT-16: 24 giờ đầu hoặc khi còn chờ duyệt
    public bool CanEdit(DateTime nowUtc) => Status == ReviewStatus.Pending || nowUtc < CreatedAtUtc + EditWindow;

    // BR-CNT-14: "Nguyễn Văn An" → "Nguyễn A."; không lộ họ tên đầy đủ
    public static string ShortAuthorName(string fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        if (string.Equals(fullName, AuthPolicy.DeletedAccountName, StringComparison.Ordinal))
        {
            return fullName;
        }

        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length < 2 ? string.Concat(parts) : $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}.";
    }
}
