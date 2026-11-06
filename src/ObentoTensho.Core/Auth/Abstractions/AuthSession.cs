namespace ObentoTensho.Core.Auth.Abstractions;

public sealed record AuthSession(AccessToken AccessToken, string RefreshToken, DateTime RefreshTokenExpiresAtUtc);
