using System.Globalization;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Customers.Abstractions;

namespace ObentoTensho.Core.Customers.Services;

public sealed class CustomerExportSource(ICustomerRepository customers) : IDataExportSource
{
    public const int Limit = 100_000;

    public DataExportKind Kind => DataExportKind.Customers;

    public string SheetName => "Khách hàng";

    public string FilePrefix => "khach-hang";

    public IReadOnlyList<string> Columns { get; } =
        ["Họ tên", "Email", "Điện thoại", "Hạng", "Điểm hiện có", "Đang hoạt động", "Ngày đăng ký (UTC)"];

    public async Task<IReadOnlyList<IReadOnlyList<string>>> LoadRowsAsync(CancellationToken cancellationToken) =>
        (await customers.ListForExportAsync(Limit, cancellationToken))
            .Select(IReadOnlyList<string> (u) =>
            [
                u.FullName,
                u.Email,
                u.PhoneNumber ?? string.Empty,
                u.MemberTier.ToString(),
                u.CurrentPoints.ToString(CultureInfo.InvariantCulture),
                u.IsActive ? "Có" : "Không",
                u.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            ])
            .ToArray();
}
