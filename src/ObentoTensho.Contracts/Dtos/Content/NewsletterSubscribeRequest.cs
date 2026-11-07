namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary><c>Website</c> là ô bẫy máy tự động, ẩn trên giao diện.</summary>
public sealed record NewsletterSubscribeRequest(string Email, string? Lang, string? Website);
