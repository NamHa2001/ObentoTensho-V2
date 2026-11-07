using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Orders.Services;

// Việc định kỳ của đơn (docs/srs/20 §4)
public sealed class OrderJobs(
    OrderDeskService desk,
    IOrderRepository orders,
    IStaffRepository staff,
    IOrderEmailOutbox emails,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private static readonly StaffRole[] Recipients = [StaffRole.SuperAdmin, StaffRole.Manager];

    // docs/srs/20 #30, BR-ORD-18: có đơn mới cần xử lý ⇒ một thư gộp cho mỗi quản lý, liệt kê mọi đơn đang cần để ý;
    // mỗi đơn chỉ làm phát sinh thư một lần
    public async Task<int> NotifyAttentionAsync(CancellationToken cancellationToken)
    {
        var items = await desk.ListAttentionAsync(cancellationToken);
        var fresh = items.Where(x => x.Order.AttentionNotifiedAtUtc is null).Select(x => x.Order.Id).ToArray();
        if (fresh.Length == 0)
        {
            return 0;
        }

        var recipients = await staff.ListActiveEmailsAsync(Recipients, cancellationToken);
        if (recipients.Count == 0)
        {
            return 0;
        }

        Order[] Of(OrderAttention.Kind kind) => items.Where(x => x.Kind == kind).Select(x => x.Order).ToArray();
        var digest = new OrderAttentionDigest(Of(OrderAttention.Kind.PendingOverdue), Of(OrderAttention.Kind.NotPickedUp), Of(OrderAttention.Kind.RefundPending));
        var now = clock.UtcNow;
        var batchKey = now.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return await unitOfWork.InTransactionAsync(async ct =>
        {
            foreach (var email in recipients)
            {
                emails.EnqueueAttentionDigest(email, digest, batchKey);
            }

            await orders.MarkAttentionNotifiedAsync(fresh, now, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return fresh.Length;
        }, cancellationToken);
    }
}
