using ObentoTensho.Contracts.Dtos.Media;

namespace ObentoTensho.Contracts.Dtos.Content;

public sealed record PublicNewsDto(
    string Slug,
    string Title,
    string? Summary,
    ImageDto? Thumbnail,
    string ContentHtml,
    string? CategorySlug,
    string AuthorDisplayName,
    DateTime PublishedAtUtc,
    string? MetaTitle,
    string? MetaDescription,
    ImageDto? OgImage);
