namespace ObentoTensho.Contracts.Dtos.Account;

/// <summary>Một thiết bị đang đăng nhập (FR-AUTH-09).</summary>
public sealed record AccountSessionDto(Guid Id, string? UserAgent, string? IpAddress, DateTime LastActiveAtUtc);
