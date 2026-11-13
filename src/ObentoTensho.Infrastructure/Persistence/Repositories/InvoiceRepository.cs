using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Invoices.Abstractions;
using ObentoTensho.Core.Invoices.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class InvoiceRepository(AppDbContext db) : IInvoiceRepository
{
    public void Add(InvoiceRequest request) => db.InvoiceRequests.Add(request);

    public Task<InvoiceRequest?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        db.InvoiceRequests.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<InvoiceRow?> FindRowAsync(Guid id, CancellationToken cancellationToken) =>
        Rows(db.InvoiceRequests.AsNoTracking().Where(i => i.Id == id)).SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasActiveForOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.InvoiceRequests.AnyAsync(i => i.OrderId == orderId && i.Status != InvoiceRequestStatus.Cancelled, cancellationToken);

    public async Task<(IReadOnlyList<InvoiceRow> Items, int Total)> ListAsync(InvoiceQuery query, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = query.Q?.Trim();
        var source = db.InvoiceRequests.AsNoTracking()
            .Where(i => (query.Status == null || i.Status == query.Status)
                && (fromUtc == null || i.CreatedAtUtc >= fromUtc)
                && (toUtc == null || i.CreatedAtUtc < toUtc));
        if (!string.IsNullOrEmpty(q))
        {
            var codes = db.Orders.Where(o => o.OrderCode.Contains(q)).Select(o => o.Id);
            source = source.Where(i => codes.Contains(i.OrderId) || i.BuyerName.Contains(q) || (i.TaxCode != null && i.TaxCode.Contains(q)));
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await Rows(source.OrderByDescending(i => i.CreatedAtUtc))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    private IQueryable<InvoiceRow> Rows(IQueryable<InvoiceRequest> source) =>
        source.Join(db.Orders, i => i.OrderId, o => o.Id, (i, o) => new InvoiceRow(i, o.OrderCode, o.Channel, o.TotalAmount, o.CreatedAtUtc));
}
