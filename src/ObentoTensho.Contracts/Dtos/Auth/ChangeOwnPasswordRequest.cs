namespace ObentoTensho.Contracts.Dtos.Auth;

public sealed record ChangeOwnPasswordRequest(string CurrentPassword, string NewPassword);
