namespace ObentoTensho.Core.Auth.Abstractions;

public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);
