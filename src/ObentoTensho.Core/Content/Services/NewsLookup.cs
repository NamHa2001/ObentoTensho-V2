using ObentoTensho.Contracts.Dtos.Content;

namespace ObentoTensho.Core.Content.Services;

// Một trong hai: bài hiển thị được, hoặc slug mới để chuyển hướng 301 (BR-CNT-41)
public sealed record NewsLookup(PublicNewsDto? Article, string? RedirectSlug);
