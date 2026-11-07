namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>Token lấy từ liên kết trong thư; chỉ POST mới đổi trạng thái (BR-CNT-22).</summary>
public sealed record NewsletterTokenRequest(string Token);
