namespace ObentoTensho.Core.Menu.Abstractions;

// Một dòng đọc từ sheet "Món": giá trị thô theo tiêu đề cột (đã bỏ khoảng trắng hai đầu, ô trống = null).
// FormulaColumns: các cột có công thức — không nhận (BR-MNU-14).
public sealed record MenuSheetRow(int RowNumber, IReadOnlyDictionary<string, string?> Cells, IReadOnlySet<string> FormulaColumns)
{
    public string? this[string column] => Cells.GetValueOrDefault(column);
}
