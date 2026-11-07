using System.Globalization;
using ClosedXML.Excel;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Spreadsheets;

// BR-CNT-25: email là dữ liệu do người ngoài nhập nên ghi qua SetText để chống formula injection
internal sealed class ClosedXmlNewsletterSpreadsheet : INewsletterSpreadsheet
{
    private const string SheetName = "Newsletter";
    private static readonly string[] Columns = ["Email", "Ngôn ngữ", "Đang nhận", "Xác nhận lúc (UTC)", "Hủy lúc (UTC)"];

    public byte[] Write(IReadOnlyList<NewsletterSubscriber> subscribers)
    {
        ArgumentNullException.ThrowIfNull(subscribers);
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet(SheetName);
        for (var c = 0; c < Columns.Length; c++)
        {
            ClosedXmlCells.SetText(sheet.Cell(1, c + 1), Columns[c]);
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns(1, Columns.Length).Width = 20;
        sheet.Column(1).Width = 36;
        for (var r = 0; r < subscribers.Count; r++)
        {
            var s = subscribers[r];
            ClosedXmlCells.SetText(sheet.Cell(r + 2, 1), s.Email);
            ClosedXmlCells.SetText(sheet.Cell(r + 2, 2), s.Lang);
            ClosedXmlCells.SetText(sheet.Cell(r + 2, 3), s.IsSubscribed ? "Có" : "Không");
            ClosedXmlCells.SetText(sheet.Cell(r + 2, 4), Time(s.ConfirmedAtUtc));
            ClosedXmlCells.SetText(sheet.Cell(r + 2, 5), Time(s.UnsubscribedAtUtc));
        }

        using var output = new MemoryStream();
        book.SaveAs(output);
        return output.ToArray();
    }

    private static string Time(DateTime? value) => value?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
}
