using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary><c>Outdated</c>: bản tiếng Việt đã đổi sau khi dịch — không hiển thị cho khách tới khi cập nhật (BR-I18N-02).</summary>
public sealed record TranslationDto(string Lang, string Name, string? ShortDesc, string? FullDescHtml, TranslationStatus Status, bool Outdated);
