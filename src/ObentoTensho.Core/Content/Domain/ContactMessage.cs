namespace ObentoTensho.Core.Content.Domain;

// docs/srs/08 §7 ContactMessages
public sealed class ContactMessage
{
    public const int MaxPerEmailPerHour = 3;
    public static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(10);

    public int Id { get; private set; }

    public required string FullName { get; init; }

    public required string Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string? IpAddress { get; init; }

    public string? Subject { get; init; }

    public required string Message { get; init; }

    public bool IsRead { get; set; }

    public DateTime? ReadAtUtc { get; set; }

    public Guid? ReadByStaffId { get; set; }

    public DateTime? RepliedAtUtc { get; set; }

    public Guid? RepliedByStaffId { get; set; }

    public required DateTime CreatedAtUtc { get; init; }
}
