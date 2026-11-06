namespace ObentoTensho.Contracts.Dtos.Auth;

public sealed record TotpSetupConfirmRequest(string ChallengeToken, string Code);
