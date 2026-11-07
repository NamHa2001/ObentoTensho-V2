namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary><c>Website</c> là ô bẫy máy tự động, ẩn trên giao diện: có giá trị ⇒ bỏ qua âm thầm (BR-CNT-33).</summary>
public sealed record ContactRequest(string FullName, string Email, string? PhoneNumber, string? Subject, string Message, string? Lang, string? Website);
