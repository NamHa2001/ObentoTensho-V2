using ObentoTensho.Contracts.Dtos.Media;
using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Content;

public sealed record AdminNewsDto(
    int Id,
    string Title,
    string Slug,
    ImageDto? Thumbnail,
    string? Summary,
    string ContentHtml,
    int? CategoryId,
    string AuthorDisplayName,
    string? MetaTitle,
    string? MetaDescription,
    ImageDto? OgImage,
    NewsStatus Status,
    DateTime? PublishedAtUtc,
    string Version);
