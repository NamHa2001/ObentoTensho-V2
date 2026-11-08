using ObentoTensho.Contracts.Dtos.Media;

namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Danh mục công khai; <c>ContentLang</c>/<c>IsFallback</c> cho biết đang hiện bản dịch hay tiếng Việt (BR-I18N-07).</summary>
public sealed record PublicCategoryDto(int Id, string Slug, string Name, int ItemCount, ImageDto? Image, string ContentLang, bool IsFallback);
