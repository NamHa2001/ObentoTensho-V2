namespace ObentoTensho.Contracts.Dtos;

/// <summary>Phong bì danh sách có phân trang <c>{ "data": [...], "meta": {...} }</c> (docs/srs/09 §2, §5).</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Data, PageMeta Meta);
