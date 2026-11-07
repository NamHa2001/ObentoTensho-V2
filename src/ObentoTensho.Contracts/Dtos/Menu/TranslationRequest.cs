namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Bản dịch en/ja (docs/srs/22). Danh mục/nhóm/tùy chọn chỉ dùng <c>Name</c>. <c>Approve</c> = người thạo ngôn ngữ đã duyệt.</summary>
public sealed record TranslationRequest(string Name, string? ShortDesc, string? FullDescHtml, bool Approve);
