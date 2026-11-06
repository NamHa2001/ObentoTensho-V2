namespace ObentoTensho.Infrastructure.Messaging;

public sealed record EmailVerificationPayload(Guid UserId, string Email, string FullName, string Language, string Token);
