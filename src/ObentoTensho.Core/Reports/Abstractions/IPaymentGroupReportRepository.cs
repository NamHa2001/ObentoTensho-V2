namespace ObentoTensho.Core.Reports.Abstractions;

// docs/srs/23 BR-PLT-09: đơn hoàn thành trong [fromUtc, toUtc) theo CompletedAtUtc; đơn app đã hủy trong khoảng theo CancelledAtUtc
public interface IPaymentGroupReportRepository
{
    Task<IReadOnlyList<PaymentGroupOrderRow>> ListAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);

    // Mới nhất trước
    Task<(IReadOnlyList<PaymentGroupOrderRow> Items, int Total)> PageAsync(DateTime fromUtc, DateTime toUtc, int page, int pageSize, CancellationToken cancellationToken);
}
