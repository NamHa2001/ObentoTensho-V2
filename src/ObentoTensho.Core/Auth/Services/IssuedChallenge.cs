using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Auth.Services;

public sealed record IssuedChallenge(StaffChallengeKind Kind, string Token, DateTime ExpiresAtUtc);
