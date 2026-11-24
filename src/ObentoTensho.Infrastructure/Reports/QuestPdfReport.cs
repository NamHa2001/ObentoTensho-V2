using System.Globalization;
using ObentoTensho.Core.Reports.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ObentoTensho.Infrastructure.Reports;

// D-04 (Q2): QuestPDF giấy phép Community — miễn phí khi doanh thu năm dưới 1 triệu USD (chủ dự án đã xác nhận).
// Chữ dùng phông Lato đi kèm gói nên máy chủ không cần cài phông; thiếu glyph tiếng Việt thì báo lỗi thay vì in ô vuông
internal sealed class QuestPdfReport : IReportPdf
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

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

    public byte[] WriteTables(string title, string subtitle, IReadOnlyList<ReportTable> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(t => t.FontSize(8));
            page.Header().Column(header =>
            {
                header.Item().Text("Obento Tensho").FontSize(9).FontColor(Colors.Grey.Darken1);
                header.Item().Text(title).FontSize(14).Bold();
                header.Item().PaddingBottom(8).Text(subtitle).FontSize(9).FontColor(Colors.Grey.Darken1);
            });
            page.Content().Column(content =>
            {
                foreach (var source in tables)
                {
                    content.Item().PaddingTop(8).PaddingBottom(4).Text(source.Name).FontSize(11).Bold();
                    content.Item().Table(table => Fill(table, source));
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

    private static void Fill(TableDescriptor table, ReportTable source)
    {
        table.ColumnsDefinition(c =>
        {
            foreach (var column in source.Columns)
            {
                c.RelativeColumn(column.Kind is ReportCellKind.Text or ReportCellKind.DateTime ? 1.4f : 1);
            }
        });
        table.Header(h =>
        {
            foreach (var column in source.Columns)
            {
                var cell = h.Cell().Background(Colors.Grey.Lighten3).Padding(3);
                (IsNumber(column) ? cell.AlignRight() : cell).Text(column.Header).Bold();
            }
        });
        foreach (var row in source.Rows)
        {
            Row(table, source.Columns, row, bold: false);
        }

        if (source.Totals is { } totals)
        {
            Row(table, source.Columns, totals, bold: true);
        }
    }

    private static void Row(TableDescriptor table, IReadOnlyList<ReportColumn> columns, IReadOnlyList<object?> values, bool bold)
    {
        for (var c = 0; c < columns.Count; c++)
        {
            var cell = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);
            var text = (IsNumber(columns[c]) ? cell.AlignRight() : cell).Text(Format(c < values.Count ? values[c] : null, columns[c].Kind));
            if (bold)
            {
                text.Bold();
            }
        }
    }

    private static bool IsNumber(ReportColumn column) => column.Kind is ReportCellKind.Count or ReportCellKind.Money or ReportCellKind.Percent;

    private static string Format(object? value, ReportCellKind kind) => value switch
    {
        null => string.Empty,
        DateTime at => at.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
        decimal number when kind == ReportCellKind.Percent => number.ToString("0.##", Vietnamese) + "%",
        decimal number => number.ToString("N0", Vietnamese),
        int count => count.ToString("N0", Vietnamese),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };
}
