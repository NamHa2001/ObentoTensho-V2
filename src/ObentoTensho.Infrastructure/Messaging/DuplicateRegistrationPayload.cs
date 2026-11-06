namespace ObentoTensho.Infrastructure.Messaging;

public sealed record DuplicateRegistrationPayload(Guid UserId, string Email, string FullName, string Language);
