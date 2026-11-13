using ObentoTensho.Core.Invoices.Domain;

namespace ObentoTensho.Core.Invoices.Abstractions;

public interface IInvoiceRepository
{
    void Add(InvoiceRequest request);

    Task<InvoiceRequest?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<InvoiceRow?> FindRowAsync(Guid id, CancellationToken cancellationToken);

    // Đơn đã có yêu cầu chưa hủy
    Task<bool> HasActiveForOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<InvoiceRow> Items, int Total)> ListAsync(InvoiceQuery query, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);
}
