namespace ObentoTensho.Contracts.Dtos.Account;

public sealed record ChangeEmailRequest(string CurrentPassword, string NewEmail);
