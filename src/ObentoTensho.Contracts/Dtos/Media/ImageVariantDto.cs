namespace ObentoTensho.Contracts.Dtos.Media;

/// <summary>Một biến thể WebP của ảnh, dùng cho <c>srcset</c> (FR-IMG-05).</summary>
public sealed record ImageVariantDto(int Width, string Url);
