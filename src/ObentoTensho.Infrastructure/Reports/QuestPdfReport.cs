using ObentoTensho.Core.Reports.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ObentoTensho.Infrastructure.Reports;

// D-04 (Q2): QuestPDF giấy phép Community — miễn phí khi doanh thu năm dưới 1 triệu USD (chủ dự án đã xác nhận).
// Chữ dùng phông Lato đi kèm gói nên máy chủ không cần cài phông; thiếu glyph tiếng Việt thì báo lỗi thay vì in ô vuông
internal sealed class QuestPdfReport : IReportPdf
{
    static QuestPdfReport()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = true;
        QuestPDF.Settings.UseSystemFonts = false;
    }

    public byte[] Write(string title, string subtitle, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(t => t.FontSize(10));
            page.Header().Column(header =>
            {
                header.Item().Text("Obento Tensho").FontSize(9).FontColor(Colors.Grey.Darken1);
                header.Item().Text(title).FontSize(16).Bold();
                header.Item().PaddingBottom(8).Text(subtitle).FontSize(9).FontColor(Colors.Grey.Darken1);
            });
            page.Content().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    for (var i = 0; i < columns.Count; i++)
                    {
                        c.RelativeColumn();
                    }
                });
                table.Header(h =>
                {
                    foreach (var column in columns)
                    {
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(column).Bold();
                    }
                });
                foreach (var row in rows)
                {
                    foreach (var cell in row)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cell);
                    }
                }
            });
            page.Footer().AlignRight().Text(t =>
            {
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        })).GeneratePdf();
    }
}
