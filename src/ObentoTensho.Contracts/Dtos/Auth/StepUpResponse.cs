namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>Gửi lại trong header <c>X-Step-Up-Token</c>; dùng được nhiều lần tới khi hết hạn.</summary>
public sealed record StepUpResponse(string Token, DateTime ExpiresAtUtc);
