namespace ObentoTensho.Contracts.Dtos.Account;

/// <summary>Mã và token tra cứu trong thư xác nhận gửi tới chính email của tài khoản (BR-AUTH-17).</summary>
public sealed record ClaimGuestRequest(string Code, string Token);
