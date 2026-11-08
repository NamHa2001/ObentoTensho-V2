using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Menu.Abstractions;

// Đọc/ghi tệp Excel thực đơn (BR-MNU-14). Read trả lỗi FILE_INVALID (không phải xlsx, sai tiêu đề)
// hoặc FILE_TOO_LARGE (quá 2 MB, quá 1.000 dòng, giải nén quá lớn); không ném ngoại lệ với tệp hỏng.
public interface IMenuSpreadsheet
{
    byte[] Write(MenuWorkbook workbook);

    Result<MenuSheetData> Read(Stream content);
}
