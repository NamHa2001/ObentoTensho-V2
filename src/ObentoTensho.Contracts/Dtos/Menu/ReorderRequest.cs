namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Thứ tự mới: danh sách Id theo thứ tự hiển thị, cập nhật bằng một lần lưu (BR-MNU-15).</summary>
public sealed record ReorderRequest(IReadOnlyList<int> Ids);
