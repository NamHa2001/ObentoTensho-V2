namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>Access token trả trong body; refresh token chỉ nằm trong cookie HttpOnly (SEC-05).</summary>
public sealed record AccessTokenResponse(string AccessToken, DateTime ExpiresAtUtc);
