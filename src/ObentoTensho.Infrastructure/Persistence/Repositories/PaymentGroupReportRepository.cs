using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Reports.Abstractions;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class PaymentGroupReportRepository(AppDbContext db) : IPaymentGroupReportRepository
{
    public async Task<IReadOnlyList<PaymentGroupOrderRow>> ListAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        await Project(Source(fromUtc, toUtc).OrderBy(At)).ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<PaymentGroupOrderRow> Items, int Total)> PageAsync(DateTime fromUtc, DateTime toUtc, PaymentGroup? group, int page, int pageSize, CancellationToken cancellationToken)
    {
        var source = Source(fromUtc, toUtc);
        if (group is { } only)
        {
            source = source.Where(o => o.PaymentGroup == only);
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await Project(source.OrderByDescending(At).ThenBy(o => o.OrderCode).Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    // Mốc thời gian của dòng: lúc hoàn thành, hoặc lúc hủy với đơn app đã hủy
    private static readonly System.Linq.Expressions.Expression<Func<Order, DateTime?>> At =
        o => o.Status == OrderStatus.Completed ? o.CompletedAtUtc : o.CancelledAtUtc;

    // BR-PLT-09: đơn hoàn thành trong khoảng; đơn app đã hủy trong khoảng vẫn hiện ở bảng chi tiết
    private IQueryable<Order> Source(DateTime fromUtc, DateTime toUtc) =>
        db.Orders.AsNoTracking().Where(o =>
            (o.Status == OrderStatus.Completed && o.CompletedAtUtc >= fromUtc && o.CompletedAtUtc < toUtc)
            || (o.Channel == OrderChannel.Platform && o.Status == OrderStatus.Cancelled && o.CancelledAtUtc >= fromUtc && o.CancelledAtUtc < toUtc));

    private static IQueryable<PaymentGroupOrderRow> Project(IQueryable<Order> source) =>
        source.Select(o => new PaymentGroupOrderRow(
            o.Id,
            o.OrderCode,
            o.Status == OrderStatus.Completed ? o.CompletedAtUtc!.Value : o.CancelledAtUtc!.Value,
            o.Status,
            o.PaymentGroup,
            o.PaymentMethod,
            o.Platform,
            o.ExternalOrderCode,
            o.SubTotal,
            o.PlatformDiscountPercent,
            o.MemberDiscount + o.VoucherDiscount + o.PlatformDiscount,
            (decimal?)o.Revenue!.NetRevenue,
            (decimal?)o.Revenue!.DeductionAmount,
            (decimal?)o.Revenue!.VatAmount,
            (decimal?)o.Revenue!.PitAmount,
            (decimal?)o.Revenue!.ReceivedAmount));
}
