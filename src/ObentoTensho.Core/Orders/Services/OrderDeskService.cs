using System.Globalization;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Orders.Services;

// FR-ORD-02/03/05: thu ngân (danh sách, gọi xác nhận, thanh toán, giao món, hoàn tiền) và bếp (Kanban)
public sealed class OrderDeskService(
    IOrderRepository orders,
    OrderTransitions transitions,
    IOrderEmailOutbox emails,
    IActionThrottle throttle,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public const int MaxPageSize = 100;

    // Màn hình bếp chỉ lấy đơn đang mở (docs/srs/12 §10); quán nhỏ nên 200 là trần an toàn
    private const int KitchenLimit = 200;

    // BR-CUS-09: dùng chung giới hạn "Hiện liên hệ" với đặt bàn — 100 lần/giờ mỗi nhân viên
    private const int RevealLimitPerHour = 100;
    private const string Entity = "order";
    private const int RefundNoteMaxLength = 300;

    public async Task<PagedResponse<AdminOrderDto>> ListAsync(OrderQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        DateTime? fromUtc = query.From is { } from ? clock.ToUtc(from, TimeOnly.MinValue) : null;
        DateTime? toUtc = query.To is { } to ? clock.ToUtc(to.AddDays(1), TimeOnly.MinValue) : null;
        var (items, total) = await orders.ListAsync(query with { Page = page, PageSize = pageSize }, fromUtc, toUtc, cancellationToken);
        return new PagedResponse<AdminOrderDto>(items.Select(o => o.ToAdmin(clock)).ToArray(), new PageMeta(page, pageSize, total));
    }

    // FR-ORD-05, BR-ORD-21: đơn chờ chỉ hiện khi đã gọi xác nhận hoặc không phải gọi
    public async Task<IReadOnlyList<KitchenOrderDto>> KitchenAsync(CancellationToken cancellationToken) =>
        (await orders.ListOpenAsync(KitchenLimit, cancellationToken))
            .Where(o => o.ReadyForKitchen)
            .Select(o => o.ToKitchen(clock))
            .ToArray();

    public async Task<Result<AdminOrderDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await orders.FindForUpdateAsync(id, withItems: true, cancellationToken) is not { } order)
        {
            return Result.Failure<AdminOrderDetailDto>(ErrorCodes.NotFound);
        }

        return Result.Success(order.ToDetail(clock, await orders.GetHistoryAsync(id, cancellationToken)));
    }

    // BR-ORD-21, BR-CUS-09: xem liên hệ đầy đủ để gọi — mỗi lần đều ghi audit, có giới hạn theo nhân viên
    public async Task<Result<OrderContactDto>> RevealContactAsync(Guid id, Guid staffId, CancellationToken cancellationToken)
    {
        var key = $"reveal-contact:{staffId:N}";
        if (throttle.Count(key) >= RevealLimitPerHour)
        {
            return Result.Failure<OrderContactDto>(ErrorCodes.RateLimited);
        }

        if (await orders.FindForUpdateAsync(id, withItems: false, cancellationToken) is not { } order)
        {
            return Result.Failure<OrderContactDto>(ErrorCodes.NotFound);
        }

        throttle.Increment(key, TimeSpan.FromHours(1));
        audit.Record(new AuditEntry("orders.order.contact_revealed", Entity, Id(order)));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new OrderContactDto(order.CustomerPhone, order.CustomerEmail));
    }

    // BR-ORD-21: "Đã gọi xác nhận" — điều kiện để bếp nhận đơn thuộc diện phải gọi
    public Task<Result<AdminOrderDto>> ConfirmContactAsync(Guid id, Guid staffId, string? note, CancellationToken cancellationToken) =>
        UpdateAsync(id, order =>
        {
            if (order.Status != OrderStatus.Pending)
            {
                return Result.Failure(ErrorCodes.InvalidTransition);
            }

            order.ContactConfirmedAtUtc = clock.UtcNow;
            order.ContactConfirmedByStaffId = staffId;
            audit.Record(new AuditEntry("orders.order.contact_confirmed", Entity, Id(order), After: new { note = Clean(note) }));
            return Result.Success();
        }, cancellationToken);

    // DB-23: ExpectedStatus phải khớp — hai người bấm cùng lúc thì chỉ một người thắng. Quyền theo bước (SEC-12).
    public Task<Result<AdminOrderDto>> ChangeStatusAsync(Guid id, ChangeOrderStatusRequest request, Guid staffId, StaffRole role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Status) || !StaffCapabilities.Has(role, OrderStateMachine.RequiredCapability(request.Status)))
        {
            return Task.FromResult(Result.Failure<AdminOrderDto>(Enum.IsDefined(request.Status) ? ErrorCodes.Forbidden : ErrorCodes.ValidationFailed));
        }

        return UpdateAsync(id, order => order.Status != request.ExpectedStatus
            ? Result.Failure(ErrorCodes.InvalidTransition)
            : transitions.Apply(order, request.Status, OrderActor.Staff, staffId, request.Reason), cancellationToken);
    }

    // FR-ORD-03, BR-ORD-09: Unpaid → Paid đúng một lần; bấm lại trả kết quả cũ; đơn đã hủy không nhận thanh toán
    public async Task<Result<AdminOrderDto>> MarkPaidAsync(Guid id, Guid staffId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var marked = await orders.TryMarkPaidAsync(id, staffId, now, cancellationToken);
        if (await orders.FindForUpdateAsync(id, withItems: true, cancellationToken) is not { } order)
        {
            return Result.Failure<AdminOrderDto>(ErrorCodes.NotFound);
        }

        if (!marked)
        {
            return order.PaymentStatus == PaymentStatus.Paid
                ? Result.Success(order.ToAdmin(clock))
                : Result.Failure<AdminOrderDto>(ErrorCodes.InvalidTransition);
        }

        audit.Record(new AuditEntry("orders.order.paid", Entity, Id(order), After: new { order.TotalAmount, order.PaymentMethod }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(order.ToAdmin(clock));
    }

    // BR-ORD-16: đơn đã thu tiền rồi hủy ⇒ ghi lại việc đã hoàn tiền (chuyển khoản lại/trả tiền mặt), báo khách
    public Task<Result<AdminOrderDto>> RecordRefundAsync(Guid id, Guid staffId, string? note, CancellationToken cancellationToken)
    {
        var clean = Clean(note);
        if (clean is null || clean.Length > RefundNoteMaxLength)
        {
            return Task.FromResult(Result.Failure<AdminOrderDto>(ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["note"] = [clean is null ? FieldErrorCodes.Required : FieldErrorCodes.TooLong],
            }));
        }

        return UpdateAsync(id, order =>
        {
            if (!order.RefundPending)
            {
                return Result.Failure(ErrorCodes.InvalidTransition);
            }

            order.RefundedAtUtc = clock.UtcNow;
            order.RefundNote = clean;
            order.RefundedByStaffId = staffId;
            audit.Record(new AuditEntry("orders.order.refunded", Entity, Id(order), After: new { order.TotalAmount, note = clean }));
            emails.EnqueueRefunded(order);
            return Result.Success();
        }, cancellationToken);
    }

    private Task<Result<AdminOrderDto>> UpdateAsync(Guid id, Func<Order, Result> change, CancellationToken cancellationToken) =>
        unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (await orders.FindForUpdateAsync(id, withItems: true, ct) is not { } order)
            {
                return Result.Failure<AdminOrderDto>(ErrorCodes.NotFound);
            }

            var changed = change(order);
            if (!changed.IsSuccess)
            {
                return Result.Failure<AdminOrderDto>(changed.ErrorCode ?? ErrorCodes.ValidationFailed, changed.FieldErrors);
            }

            order.UpdatedAtUtc = clock.UtcNow;
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(order.ToAdmin(clock));
        }, cancellationToken);

    private static string Id(Order order) => order.Id.ToString("D", CultureInfo.InvariantCulture);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
