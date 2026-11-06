using ObentoTensho.Core.Auth.Abstractions;

namespace ObentoTensho.Core.Auth.Services;

public sealed record StaffTotpEnabled(AuthSession Session, IReadOnlyList<string> RecoveryCodes);
