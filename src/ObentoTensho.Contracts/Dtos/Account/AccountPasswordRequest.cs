namespace ObentoTensho.Contracts.Dtos.Account;

/// <summary>Xác nhận bằng mật khẩu trước thao tác không đảo ngược được.</summary>
public sealed record AccountPasswordRequest(string Password);
