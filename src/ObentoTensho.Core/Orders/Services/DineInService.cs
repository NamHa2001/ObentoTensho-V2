using System.Globalization;
using ObentoTensho.Contracts.Dtos.DineIn;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Invoices.Services;
using ObentoTensho.Core.Loyalty.Services;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Realtime.Abstractions;
using ObentoTensho.Core.Realtime.Domain;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Tables.Domain;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Vouchers.Domain;
using ObentoTensho.Core.Vouchers.Services;

namespace ObentoTensho.Core.Orders.Services;

// Gọi món tại bàn (chủ quán 2026-11-13). Một bàn một phiếu đang mở (chỉ mục UNIQUE có lọc giữ khi hai người mở cùng lúc);
// phiếu là một đơn kênh DineIn: gọi nhiều đợt, mỗi đợt gửi bếp ngay; hủy món đã gửi bếp cần quyền quản lý;
// thanh toán dùng chung giá/giảm theo hạng/voucher/tích điểm với đơn mang về (BR-ORD-03/08). Không VAT, không phí phục vụ.
public sealed class DineInService(
    IOrderRepository orders,
    ITableLayoutRepository tables,
    IUserRepository users,
    OrderCart cart,
    OrderTransitions transitions,
    LoyaltyService loyalty,
    VoucherEvaluator vouchers,
    InvoiceService invoices,
    ISecureTokenService secureTokens,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IRealtimeEvents realtime,
    IClock clock)
{
    public const int MaxGuests = 50;
    public const int MaxLinesPerRound = 30;
    public const int NoteMaxLength = 500;
    public const int ReasonMaxLength = 200;
    public const int CounterNameMaxLength = 50;
    private const string CounterGuest = "Khách mang về";
    private static readonly char[] Markup = ['<', '>'];
    private const string Entity = "order";
    private const string Language = "vi";

    public async Task<IReadOnlyList<DineInTableDto>> TablesAsync(CancellationToken cancellationToken)
    {
        var bills = (await orders.ListOpenDineInAsync(cancellationToken)).Where(o => o.TableId is not null).ToDictionary(o => o.TableId!.Value);
        var areas = (await tables.ListAreasAsync(cancellationToken)).Where(a => a.IsActive).ToDictionary(a => a.Id, a => a.Name);
        return (await tables.ListTablesAsync(areaId: null, cancellationToken))
            .Where(t => t.IsActive && areas.ContainsKey(t.AreaId))
            .OrderBy(t => areas[t.AreaId], StringComparer.Ordinal).ThenBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => bills.GetValueOrDefault(t.Id) is { } bill
                ? new DineInTableDto(t.Id, t.Name, t.AreaId, areas[t.AreaId], t.MaxCapacity, t.CurrentStatus, bill.Id, bill.OrderCode, bill.GuestCount, bill.CreatedAtUtc, bill.SubTotal, Unserved(bill).Count())
                : new DineInTableDto(t.Id, t.Name, t.AreaId, areas[t.AreaId], t.MaxCapacity, t.CurrentStatus, null, null, null, null, null, 0))
            .ToArray();
    }

    // Bàn "Dọn dẹp" sau khi khách về: nhân viên phục vụ báo đã dọn xong (bàn còn phiếu mở thì giữ "Có khách")
    public async Task<Result> MarkCleanAsync(int tableId, CancellationToken cancellationToken)
    {
        if (await tables.FindTableAsync(tableId, cancellationToken) is not { IsActive: true } table)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        if (table.CurrentStatus != TableStatus.Cleaning || await orders.TableHasOpenBillAsync(tableId, cancellationToken))
        {
            return Result.Failure(ErrorCodes.InvalidTransition);
        }

        SetTable(table, TableStatus.Available);
        return await unitOfWork.TrySaveAsync(ErrorCodes.PreconditionFailed, cancellationToken);
    }

    public async Task<Result<DineInBillDto>> GetAsync(Guid billId, CancellationToken cancellationToken) =>
        await orders.FindOpenDineInAsync(billId, cancellationToken) is { } bill
            ? Result.Success(await ToBillAsync(bill, cancellationToken))
            : Result.Failure<DineInBillDto>(ErrorCodes.NotFound);

    // Đơn mang về tại quầy đang mở: chưa thanh toán, hoặc đã trả tiền mà bếp chưa ra hết món
    public async Task<IReadOnlyList<CounterBillDto>> CounterAsync(CancellationToken cancellationToken) =>
        (await orders.ListOpenDineInAsync(cancellationToken))
            .Where(o => o.Channel == OrderChannel.Counter)
            .Select(o => new CounterBillDto(o.Id, o.OrderCode, o.CustomerName, o.CreatedAtUtc, o.SubTotal, Unserved(o).Count(), o.PaymentStatus == PaymentStatus.Paid))
            .ToArray();

    // Khách mua mang đi tại quầy: phiếu không gắn bàn, gọi món và thanh toán như tại bàn
    public async Task<Result<DineInBillDto>> OpenCounterAsync(OpenCounterRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = request.CustomerName?.Trim() ?? string.Empty;
        if (name.Length > CounterNameMaxLength || name.IndexOfAny(Markup) >= 0 || request.Note is { Length: > NoteMaxLength })
        {
            return Result.Failure<DineInBillDto>(ErrorCodes.ValidationFailed, Field(request.Note is { Length: > NoteMaxLength } ? "note" : "customerName", FieldErrorCodes.InvalidFormat));
        }

        var now = clock.UtcNow;
        var bill = NewBill(null, request.Note, await orders.NextDailySeqAsync(clock.TodayLocal, cancellationToken), now);
        bill.CustomerName = name.Length > 0 ? name : CounterGuest;
        orders.Add(bill);
        orders.AddHistory(new OrderStatusHistory { OrderId = bill.Id, ToStatus = OrderStatus.Preparing, ChangedByType = OrderActor.Staff, ChangedById = staffId, ChangedAtUtc = now });
        realtime.Stage(RealtimeMessages.OrderCreated(bill));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToBill(bill, Label(bill, [])));
    }

    // Bếp: phiếu còn món chưa ra (chỉ món chưa ra, chưa hủy)
    public async Task<IReadOnlyList<DineInKitchenTicketDto>> KitchenAsync(CancellationToken cancellationToken)
    {
        var names = await TableNamesAsync(cancellationToken);
        return (await orders.ListOpenDineInAsync(cancellationToken))
            .Select(o => new DineInKitchenTicketDto(o.Id, o.OrderCode, Label(o, names), Unserved(o)
                .OrderBy(i => i.SentAtUtc).ThenBy(i => i.Id)
                .Select(i => new DineInKitchenLineDto(i.Id, i.Round, i.ItemName, i.Quantity, OptionLabels(i), i.Note, i.SentAtUtc ?? o.CreatedAtUtc))
                .ToArray()))
            .Where(t => t.Items.Count > 0)
            .ToArray();
    }

    public async Task<Result<DineInBillDto>> OpenAsync(OpenTableRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (ValidateOpen(request) is { Count: > 0 } errors)
        {
            return Result.Failure<DineInBillDto>(ErrorCodes.ValidationFailed, errors);
        }

        if (await tables.FindTableAsync(request.TableId, cancellationToken) is not { IsActive: true } table)
        {
            return Result.Failure<DineInBillDto>(ErrorCodes.NotFound);
        }

        if (await orders.TableHasOpenBillAsync(table.Id, cancellationToken))
        {
            return Result.Failure<DineInBillDto>(ErrorCodes.TableConflict);
        }

        var now = clock.UtcNow;
        var bill = NewBill(table, request.Note, await orders.NextDailySeqAsync(clock.TodayLocal, cancellationToken), now);
        bill.GuestCount = request.GuestCount;
        orders.Add(bill);
        orders.AddHistory(new OrderStatusHistory
        {
            OrderId = bill.Id,
            ToStatus = OrderStatus.Preparing,
            ChangedByType = OrderActor.Staff,
            ChangedById = staffId,
            ChangedAtUtc = now,
        });
        SetTable(table, TableStatus.Occupied);
        realtime.Stage(RealtimeMessages.OrderCreated(bill));
        var saved = await unitOfWork.TrySaveAsync(ErrorCodes.TableConflict, cancellationToken);
        return saved.IsSuccess ? Result.Success(ToBill(bill, table.Name)) : Result.Failure<DineInBillDto>(saved.ErrorCode ?? ErrorCodes.TableConflict);
    }

    private static Dictionary<string, string[]> ValidateOpen(OpenTableRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request.GuestCount is < 1 or > MaxGuests)
        {
            errors["guestCount"] = [FieldErrorCodes.OutOfRange];
        }

        if (request.Note is { Length: > NoteMaxLength })
        {
            errors["note"] = [FieldErrorCodes.TooLong];
        }

        return errors;
    }

    // table = null ⇒ đơn mang về tại quầy
    private Order NewBill(DiningTable? table, string? note, int dailySeq, DateTime now) =>
        new()
        {
            OrderCode = OrderCodes.New(),
            DailySeq = dailySeq,
            AccessTokenHash = secureTokens.Create().Hash,
            CustomerName = table is null ? CounterGuest : TableLabel(table.Name),
            CustomerPhone = string.Empty,
            CustomerEmail = string.Empty,
            Lang = Language,
            PickupAtUtc = now,
            Channel = table is null ? OrderChannel.Counter : OrderChannel.DineIn,
            TableId = table?.Id,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            SubTotal = 0,
            TotalAmount = 0,
            PaymentMethod = PaymentMethod.Cash,
            Status = OrderStatus.Preparing,
            CreatedAtUtc = now,
        };

    // Một đợt gọi món: gửi bếp ngay, giá đọc lại từ thực đơn
    public async Task<Result<DineInBillDto>> AddItemsAsync(Guid billId, AddDineInItemsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Items is not { Count: > 0 and <= MaxLinesPerRound })
        {
            return Result.Failure<DineInBillDto>(ErrorCodes.ValidationFailed, Field("items", FieldErrorCodes.OutOfRange));
        }

        if (await orders.FindOpenDineInAsync(billId, cancellationToken) is not { } bill)
        {
            return Result.Failure<DineInBillDto>(ErrorCodes.NotFound);
        }

        if (bill.PaymentStatus == PaymentStatus.Paid)
        {
            return Result.Failure<DineInBillDto>(ErrorCodes.InvalidTransition);
        }

        var now = clock.UtcNow;
        var round = bill.Items.Count == 0 ? 1 : bill.Items.Max(i => i.Round) + 1;
        var priced = await cart.PriceAsync(request.Items, Language, cancellationToken, new OrderCart.DineInRound(round, now));
        if (!priced.IsValid)
        {
            return Result.Failure<DineInBillDto>(ErrorCodes.ItemUnavailable, priced.Lines
                .Where(l => l.ErrorCode is not null)
                .ToDictionary(l => $"items[{l.Index}]", l => new[] { l.ErrorCode! }, StringComparer.Ordinal));
        }

        foreach (var item in priced.Items)
        {
            bill.Items.Add(item);
        }

        Recalculate(bill, now);
        realtime.Stage(RealtimeMessages.OrderChanged(bill));
        return await SaveBillAsync(bill, cancellationToken);
    }

    public async Task<Result<DineInBillDto>> VoidItemAsync(Guid billId, long itemId, DineInReasonRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Reason(request.Reason) is not { } reason)
        {
            return Result.Failure<DineInBillDto>(ErrorCodes.ValidationFailed, Field("reason", string.IsNullOrWhiteSpace(request.Reason) ? FieldErrorCodes.Required : FieldErrorCodes.TooLong));
        }

        if (await orders.FindOpenDineInAsync(billId, cancellationToken) is not { } bill
            || bill.Items.FirstOrDefault(i => i.Id == itemId) is not { IsVoided: false } item)
        {
            return Result.Failure<DineInBillDto>(ErrorCodes.NotFound);
        }

        if (bill.PaymentStatus == PaymentStatus.Paid)
        {
            return Result.Failure<DineInBillDto>(ErrorCodes.InvalidTransition);
        }

        var now = clock.UtcNow;
        item.VoidedAtUtc = now;
        item.VoidReason = reason;
        item.VoidedByStaffId = staffId;
        Recalculate(bill, now);
        audit.Record(new AuditEntry("orders.dine_in.item_voided", Entity, Id(bill), After: new { itemId, item.ItemName, item.Quantity, reason }));
        realtime.Stage(RealtimeMessages.OrderChanged(bill));
        return await SaveBillAsync(bill, cancellationToken);
    }

    // Bếp ra món (hoặc nhân viên đã bưng ra); gọi lại cho món đã ra thì giữ nguyên
    public async Task<Result<DineInBillDto>> ServeItemAsync(Guid billId, long itemId, CancellationToken cancellationToken)
    {
        if (await orders.FindOpenDineInAsync(billId, cancellationToken) is not { } bill
            || bill.Items.FirstOrDefault(i => i.Id == itemId) is not { IsVoided: false } item)
        {
            return Result.Failure<DineInBillDto>(ErrorCodes.NotFound);
        }

        if (item.ServedAtUtc is null)
        {
            item.ServedAtUtc = clock.UtcNow;
            bill.UpdatedAtUtc = item.ServedAtUtc;
            realtime.Stage(RealtimeMessages.OrderChanged(bill));
            if (bill.Channel == OrderChannel.Counter && bill.PaymentStatus == PaymentStatus.Paid && !Unserved(bill).Any())
            {
                await transitions.ApplyAsync(bill, OrderStatus.Completed, OrderActor.Staff, actorId: null, reason: null, cancellationToken);
            }
        }

        return await SaveBillAsync(bill, cancellationToken);
    }

    // Chuyển sang bàn trống; bàn cũ chuyển "Dọn dẹp"
    public async Task<Result<DineInBillDto>> MoveAsync(Guid billId, MoveTableRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await orders.FindOpenDineInAsync(billId, cancellationToken) is not { } bill
            || await tables.FindTableAsync(request.TableId, cancellationToken) is not { IsActive: true } target)
        {
            return Result.Failure<DineInBillDto>(ErrorCodes.NotFound);
        }

        if (bill.Channel != OrderChannel.DineIn)
        {
            return Result.Failure<DineInBillDto>(ErrorCodes.InvalidTransition);
        }

        if (target.Id == bill.TableId || await orders.TableHasOpenBillAsync(target.Id, cancellationToken))
        {
            return Result.Failure<DineInBillDto>(ErrorCodes.TableConflict);
        }

        if (bill.TableId is { } fromId && await tables.FindTableAsync(fromId, cancellationToken) is { } from)
        {
            SetTable(from, TableStatus.Cleaning);
        }

        bill.TableId = target.Id;
        bill.CustomerName = bill.UserId is null ? TableLabel(target.Name) : bill.CustomerName;
        bill.UpdatedAtUtc = clock.UtcNow;
        SetTable(target, TableStatus.Occupied);
        realtime.Stage(RealtimeMessages.OrderChanged(bill));
        return await SaveBillAsync(bill, cancellationToken, ErrorCodes.TableConflict);
    }

    // Gộp phiếu bàn khác vào phiếu này: chuyển món (giữ đợt và trạng thái), cộng số khách; phiếu nguồn hủy, bàn nguồn dọn dẹp
    public Task<Result<DineInBillDto>> MergeAsync(Guid billId, MergeBillRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (request.FromBillId == billId)
            {
                return Result.Failure<DineInBillDto>(ErrorCodes.TableConflict);
            }

            if (await orders.FindOpenDineInAsync(billId, ct) is not { } bill || await orders.FindOpenDineInAsync(request.FromBillId, ct) is not { } source)
            {
                return Result.Failure<DineInBillDto>(ErrorCodes.NotFound);
            }

            if (bill.Channel != OrderChannel.DineIn || source.Channel != OrderChannel.DineIn)
            {
                return Result.Failure<DineInBillDto>(ErrorCodes.InvalidTransition);
            }

            var names = await TableNamesAsync(ct);
            foreach (var item in source.Items.ToArray())
            {
                source.Items.Remove(item);
                bill.Items.Add(item);
            }

            var now = clock.UtcNow;
            bill.GuestCount = (bill.GuestCount ?? 0) + (source.GuestCount ?? 0);
            Recalculate(bill, now);
            Recalculate(source, now);
            var cancelled = await transitions.ApplyAsync(source, OrderStatus.Cancelled, OrderActor.Staff, staffId, $"Gộp vào {Label(bill, names)}", ct);
            if (!cancelled.IsSuccess)
            {
                return Result.Failure<DineInBillDto>(cancelled.ErrorCode ?? ErrorCodes.InvalidTransition);
            }

            if (source.TableId is { } fromId && await tables.FindTableAsync(fromId, ct) is { } from)
            {
                SetTable(from, TableStatus.Cleaning);
            }

            realtime.Stage(RealtimeMessages.OrderChanged(bill));
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(ToBill(bill, Label(bill, names)));
        }, cancellationToken);
    }

    public async Task<Result<DineInQuoteDto>> QuoteAsync(Guid billId, DineInQuoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await orders.FindOpenDineInAsync(billId, cancellationToken) is not { } bill)
        {
            return Result.Failure<DineInQuoteDto>(ErrorCodes.NotFound);
        }

        var priced = await PriceAsync(bill, request.VoucherCode, request.MemberPhone, cancellationToken);
        return priced.IsSuccess
            ? Result.Success(new DineInQuoteDto(priced.Value.Totals.SubTotal, priced.Value.Totals.MemberDiscount, priced.Value.Totals.VoucherDiscount, priced.Value.Totals.TotalAmount, priced.Value.Member?.FullName))
            : Result.Failure<DineInQuoteDto>(priced.ErrorCode ?? ErrorCodes.ValidationFailed, priced.FieldErrors);
    }

    // Thanh toán và đóng bàn: giữ lượt voucher, ghi đã thu, hoàn thành (cộng điểm thành viên), bàn chuyển "Dọn dẹp"
    public Task<Result<DineInReceiptDto>> CheckoutAsync(Guid billId, DineInCheckoutRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (CheckoutErrors(request) is { } errors)
            {
                return Result.Failure<DineInReceiptDto>(ErrorCodes.ValidationFailed, errors);
            }

            if (await orders.FindOpenDineInAsync(billId, ct) is not { } bill)
            {
                return Result.Failure<DineInReceiptDto>(ErrorCodes.NotFound);
            }

            if (!bill.Items.Any(i => !i.IsVoided) || bill.PaymentStatus == PaymentStatus.Paid)
            {
                return Result.Failure<DineInReceiptDto>(ErrorCodes.InvalidTransition);
            }

            var priced = await PriceAsync(bill, request.VoucherCode, request.MemberPhone, ct);
            if (!priced.IsSuccess)
            {
                return Result.Failure<DineInReceiptDto>(priced.ErrorCode ?? ErrorCodes.ValidationFailed, priced.FieldErrors);
            }

            var (totals, voucher, member) = priced.Value;
            if (voucher is not null && !await vouchers.ReserveAsync(voucher, member?.Id, ct))
            {
                return Result.Failure<DineInReceiptDto>(ErrorCodes.VoucherExhausted);
            }

            var now = clock.UtcNow;
            ApplyPayment(bill, priced.Value, request.PaymentMethod, staffId, now);
            var waiting = bill.Channel == OrderChannel.Counter && Unserved(bill).Any();
            var completed = waiting ? Result.Success() : await transitions.ApplyAsync(bill, OrderStatus.Completed, OrderActor.Staff, staffId, reason: null, ct);
            if (!completed.IsSuccess)
            {
                return Result.Failure<DineInReceiptDto>(completed.ErrorCode ?? ErrorCodes.InvalidTransition);
            }

            realtime.Stage(RealtimeMessages.OrderChanged(bill));

            var names = await TableNamesAsync(ct);
            if (bill.TableId is { } tableId && await tables.FindTableAsync(tableId, ct) is { } table)
            {
                SetTable(table, TableStatus.Cleaning);
            }

            audit.Record(new AuditEntry("orders.order.paid", Entity, Id(bill), After: new { bill.TotalAmount, bill.PaymentMethod }));
            if (request.Invoice is not null)
            {
                invoices.Attach(bill.Id, request.Invoice, staffId);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(new DineInReceiptDto(
                bill.OrderCode, Label(bill, names), bill.GuestCount ?? 0, Lines(bill), bill.SubTotal, bill.MemberDiscount,
                bill.VoucherDiscount, bill.TotalAmount, bill.PaymentMethod, now, member?.FullName, bill.PointsEarned));
        }, cancellationToken);
    }

    private IReadOnlyDictionary<string, string[]>? CheckoutErrors(DineInCheckoutRequest request)
    {
        if (!Enum.IsDefined(request.PaymentMethod))
        {
            return Field("paymentMethod", FieldErrorCodes.OutOfRange);
        }

        return request.Invoice is not null && invoices.Validate(request.Invoice) is { Count: > 0 } errors ? errors : null;
    }

    private static void ApplyPayment(Order bill, (OrderPricing.Totals Totals, Voucher? Voucher, User? Member) priced, PaymentMethod method, Guid staffId, DateTime now)
    {
        var (totals, voucher, member) = priced;
        if (member is not null)
        {
            bill.UserId = member.Id;
            bill.CustomerName = member.FullName;
            bill.CustomerPhone = member.PhoneNumber ?? string.Empty;
        }

        bill.SubTotal = totals.SubTotal;
        bill.MemberDiscount = totals.MemberDiscount;
        bill.VoucherDiscount = totals.VoucherDiscount;
        bill.TotalAmount = totals.TotalAmount;
        bill.VoucherId = voucher?.Id;
        bill.VoucherCodeSnapshot = voucher?.Code;
        bill.PaymentMethod = method;
        bill.PaymentStatus = PaymentStatus.Paid;
        bill.PaidAtUtc = now;
        bill.PaidByStaffId = staffId;
    }

    // Hủy phiếu mở nhầm hoặc khách về khi chưa gọi gì: phải hủy hết món trước (hủy món cần quyền quản lý)
    public async Task<Result> CancelAsync(Guid billId, DineInReasonRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (Reason(request.Reason) is not { } reason)
            {
                return Result.Failure<bool>(ErrorCodes.ValidationFailed, Field("reason", string.IsNullOrWhiteSpace(request.Reason) ? FieldErrorCodes.Required : FieldErrorCodes.TooLong));
            }

            if (await orders.FindOpenDineInAsync(billId, ct) is not { } bill)
            {
                return Result.Failure<bool>(ErrorCodes.NotFound);
            }

            if (bill.Items.Any(i => !i.IsVoided))
            {
                return Result.Failure<bool>(ErrorCodes.BillNotEmpty);
            }

            var cancelled = await transitions.ApplyAsync(bill, OrderStatus.Cancelled, OrderActor.Staff, staffId, reason, ct);
            if (!cancelled.IsSuccess)
            {
                return Result.Failure<bool>(cancelled.ErrorCode ?? ErrorCodes.InvalidTransition);
            }

            if (bill.TableId is { } tableId && await tables.FindTableAsync(tableId, ct) is { } table)
            {
                SetTable(table, TableStatus.Available);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(true);
        }, cancellationToken);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.ErrorCode ?? ErrorCodes.InvalidTransition, result.FieldErrors);
    }

    // BR-ORD-03, BR-VCH-04: có mã ⇒ giảm theo voucher (không giảm theo hạng); không mã ⇒ thành viên giảm theo hạng
    private async Task<Result<(OrderPricing.Totals Totals, Voucher? Voucher, User? Member)>> PriceAsync(Order bill, string? voucherCode, string? memberPhone, CancellationToken cancellationToken)
    {
        User? member = null;
        if (!string.IsNullOrWhiteSpace(memberPhone))
        {
            if (!VietnamesePhoneNumber.TryNormalize(memberPhone, out var phone))
            {
                return Result.Failure<(OrderPricing.Totals, Voucher?, User?)>(ErrorCodes.ValidationFailed, Field("memberPhone", FieldErrorCodes.InvalidFormat));
            }

            member = await users.FindMemberByPhoneAsync(phone, cancellationToken);
            if (member is null)
            {
                return Result.Failure<(OrderPricing.Totals, Voucher?, User?)>(ErrorCodes.MemberNotFound);
            }
        }

        var subTotal = bill.Items.Sum(i => i.LineTotal);
        if (string.IsNullOrWhiteSpace(voucherCode))
        {
            var rate = await loyalty.MemberDiscountRateAsync(member?.Id, cancellationToken);
            return Result.Success<(OrderPricing.Totals, Voucher?, User?)>((OrderPricing.Compute(subTotal, rate, voucherDiscount: 0), null, member));
        }

        var evaluated = await vouchers.EvaluateAsync(voucherCode, subTotal, member?.Id, cancellationToken);
        return evaluated.IsSuccess
            ? Result.Success<(OrderPricing.Totals, Voucher?, User?)>((OrderPricing.Compute(subTotal, memberRate: 0, evaluated.Value.Discount), evaluated.Value.Voucher, member))
            : Result.Failure<(OrderPricing.Totals, Voucher?, User?)>(evaluated.ErrorCode ?? ErrorCodes.VoucherInvalid);
    }

    private async Task<Result<DineInBillDto>> SaveBillAsync(Order bill, CancellationToken cancellationToken, string duplicateErrorCode = ErrorCodes.PreconditionFailed)
    {
        var saved = await unitOfWork.TrySaveAsync(duplicateErrorCode, cancellationToken);
        return saved.IsSuccess
            ? Result.Success(await ToBillAsync(bill, cancellationToken))
            : Result.Failure<DineInBillDto>(saved.ErrorCode ?? ErrorCodes.PreconditionFailed);
    }

    private void SetTable(DiningTable table, TableStatus status)
    {
        if (table.CurrentStatus != status)
        {
            table.CurrentStatus = status;
            realtime.Stage(RealtimeMessages.TableChanged(table));
        }
    }

    // Phiếu đang mở chỉ có tạm tính; giảm giá tính lúc thanh toán
    private static void Recalculate(Order bill, DateTime now)
    {
        bill.SubTotal = bill.Items.Sum(i => i.LineTotal);
        bill.MemberDiscount = 0;
        bill.VoucherDiscount = 0;
        bill.TotalAmount = bill.SubTotal;
        bill.UpdatedAtUtc = now;
    }

    private async Task<DineInBillDto> ToBillAsync(Order bill, CancellationToken cancellationToken) =>
        ToBill(bill, Label(bill, await TableNamesAsync(cancellationToken)));

    private static DineInBillDto ToBill(Order bill, string tableName) =>
        new(bill.Id, bill.OrderCode, bill.TableId ?? 0, tableName, bill.GuestCount ?? 0, bill.CreatedAtUtc, Lines(bill), bill.SubTotal, bill.Note, RowVersions.Encode(bill.RowVersion), bill.Channel, bill.PaymentStatus == PaymentStatus.Paid);

    private static DineInLineDto[] Lines(Order bill) =>
        bill.Items
            .OrderBy(i => i.Round).ThenBy(i => i.Id)
            .Select(i => new DineInLineDto(
                i.Id,
                i.Round,
                i.ItemName,
                i.ItemPrice + i.OptionsPrice,
                i.Quantity,
                i.LineTotal,
                OptionLabels(i),
                i.Note,
                i.SentAtUtc ?? bill.CreatedAtUtc,
                i.ServedAtUtc,
                i.IsVoided,
                i.VoidReason))
            .ToArray();

    private static IEnumerable<OrderItem> Unserved(Order bill) => bill.Items.Where(i => !i.IsVoided && i.ServedAtUtc is null);

    private static string[] OptionLabels(OrderItem item) => item.Options.Select(x => $"{x.GroupName}: {x.OptionName}").ToArray();

    private async Task<Dictionary<int, string>> TableNamesAsync(CancellationToken cancellationToken) =>
        (await tables.ListTablesAsync(areaId: null, cancellationToken)).ToDictionary(t => t.Id, t => t.Name);

    private static string TableName(Dictionary<int, string> names, int? tableId) =>
        tableId is { } id && names.TryGetValue(id, out var name) ? TableLabel(name) : string.Empty;

    private static string TableLabel(string name) => $"Bàn {name}";

    // Tên hiển thị của phiếu: "Bàn A1", hoặc "Mang về · Lan" với đơn tại quầy
    private static string Label(Order bill, Dictionary<int, string> names) =>
        bill.Channel == OrderChannel.Counter ? $"Mang về · {bill.CustomerName}" : TableName(names, bill.TableId);

    private static string? Reason(string? raw) =>
        string.IsNullOrWhiteSpace(raw) || raw.Trim().Length > ReasonMaxLength ? null : raw.Trim();

    private static Dictionary<string, string[]> Field(string name, string code) =>
        new(StringComparer.Ordinal) { [name] = [code] };

    private static string Id(Order order) => order.Id.ToString("D", CultureInfo.InvariantCulture);
}
