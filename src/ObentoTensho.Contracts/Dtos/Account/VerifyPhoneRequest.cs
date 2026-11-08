namespace ObentoTensho.Contracts.Dtos.Account;

/// <summary>Mã 6 số trong SMS gửi tới SĐT của tài khoản.</summary>
public sealed record VerifyPhoneRequest(string Code);
