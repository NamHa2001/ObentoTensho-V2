namespace ObentoTensho.Core.Content.Domain;

// docs/srs/08 ConsentRecords, BR-CNT-26. Người đăng ký bản tin có thể chưa có tài khoản nên lưu thêm Email, UserId để trống
public sealed class ConsentRecord
{
    public const string Marketing = "marketing";
    public const string MarketingVersion = "1";

    public long Id { get; private set; }

    public Guid? UserId { get; init; }

    public string? Email { get; init; }

    public required string DocType { get; init; }

    public required string Version { get; init; }

    public required DateTime AcceptedAtUtc { get; init; }

    public string? Ip { get; init; }
}
