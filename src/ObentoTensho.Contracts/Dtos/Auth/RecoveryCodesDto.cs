namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>Mã khôi phục chỉ hiện một lần; mã cũ hết hiệu lực ngay khi tạo bộ mới.</summary>
public sealed record RecoveryCodesDto(IReadOnlyList<string> RecoveryCodes);
