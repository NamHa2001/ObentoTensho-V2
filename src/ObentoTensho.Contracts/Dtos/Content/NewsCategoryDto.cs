namespace ObentoTensho.Contracts.Dtos.Content;

public sealed record NewsCategoryDto(int Id, string Name, string Slug, int DisplayOrder, bool IsActive, string Version);
