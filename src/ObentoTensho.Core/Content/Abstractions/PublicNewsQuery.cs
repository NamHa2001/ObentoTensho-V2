namespace ObentoTensho.Core.Content.Abstractions;

public sealed record PublicNewsQuery(string? CategorySlug, string? Search, int Page, int PageSize);
