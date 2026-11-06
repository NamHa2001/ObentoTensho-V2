namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>Đăng ký tài khoản khách (FR-AUTH-01).</summary>
public sealed record RegisterRequest(
    string FullName,
    string Email,
    string PhoneNumber,
    string Password,
    DateOnly? DateOfBirth,
    string? PreferredLang);
