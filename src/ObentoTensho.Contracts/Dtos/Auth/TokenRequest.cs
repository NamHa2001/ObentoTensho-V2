namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>Token lấy từ liên kết trong email (phần <c>#token=</c>).</summary>
public sealed record TokenRequest(string Token);
