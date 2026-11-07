namespace ObentoTensho.Contracts.Dtos.Content;

public sealed record StaticPageRevisionDto(int Revision, string Title, Guid UpdatedByStaffId, DateTime UpdatedAtUtc);
