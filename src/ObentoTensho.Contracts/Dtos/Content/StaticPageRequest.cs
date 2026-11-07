namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>Sửa trang tĩnh; mỗi lần lưu tạo một phiên bản mới, khôi phục được (FR-CNT-04).</summary>
public sealed record StaticPageRequest(string Title, string ContentHtml);
