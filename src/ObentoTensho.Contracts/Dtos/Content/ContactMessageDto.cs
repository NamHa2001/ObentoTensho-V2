namespace ObentoTensho.Contracts.Dtos.Content;

public sealed record ContactMessageDto(
    int Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    string? Subject,
    string Message,
    bool IsRead,
    DateTime? ReadAtUtc,
    DateTime? RepliedAtUtc,
    DateTime CreatedAtUtc);
