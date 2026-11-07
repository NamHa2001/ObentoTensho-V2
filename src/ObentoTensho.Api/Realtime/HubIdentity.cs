namespace ObentoTensho.Api.Realtime;

// Danh tính lấy từ access token lúc xin vé; Role chỉ có với nhân viên
public sealed record HubIdentity(string Kind, Guid Id, string? Role, string SecurityStamp, DateTimeOffset ExpiresAt)
{
    public const string StaffKind = "staff";
    public const string CustomerKind = "customer";

    public bool IsStaff => string.Equals(Kind, StaffKind, StringComparison.Ordinal);

    public string Key => Kind + ":" + Id.ToString("N");
}
