using System.Globalization;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Customers.Abstractions;

namespace ObentoTensho.Core.Content.Services;

// BR-CNT-25: chạy qua cùng luồng xuất có kiểm soát với danh sách khách
public sealed class NewsletterExportSource(INewsletterRepository newsletter) : IDataExportSource
{
    public const int Limit = 100_000;

    public DataExportKind Kind => DataExportKind.NewsletterSubscribers;

    public string SheetName => "Newsletter";

    public string FilePrefix => "newsletter";

    public IReadOnlyList<string> Columns { get; } = ["Email", "Ngôn ngữ", "Đang nhận", "Xác nhận lúc (UTC)", "Hủy lúc (UTC)"];

    public async Task<IReadOnlyList<IReadOnlyList<string>>> LoadRowsAsync(CancellationToken cancellationToken) =>
        (await newsletter.ListForExportAsync(Limit, cancellationToken))
            .Select(IReadOnlyList<string> (s) => [s.Email, s.Lang, s.IsSubscribed ? "Có" : "Không", Time(s.ConfirmedAtUtc), Time(s.UnsubscribedAtUtc)])
            .ToArray();

    private static string Time(DateTime? value) => value?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
}
