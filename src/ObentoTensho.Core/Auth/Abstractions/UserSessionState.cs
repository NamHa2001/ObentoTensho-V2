namespace ObentoTensho.Core.Auth.Abstractions;

public sealed record UserSessionState(bool CanSignIn, string SecurityStamp);
