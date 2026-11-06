using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>Mã tạm chỉ dùng cho đúng bước <see cref="Kind"/>, hết hạn sau 5 phút.</summary>
public sealed record StaffChallengeResponse(StaffChallengeKind Kind, string ChallengeToken, DateTime ExpiresAtUtc);
