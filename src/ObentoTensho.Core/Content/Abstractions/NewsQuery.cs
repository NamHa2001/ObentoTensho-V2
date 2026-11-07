using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Content.Abstractions;

// Danh sách bài cho quản trị; Trash = chỉ bài trong thùng rác
public sealed record NewsQuery(NewsStatus? Status, int? CategoryId, string? Search, bool Trash, int Page, int PageSize);
