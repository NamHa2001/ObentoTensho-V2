using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Invoices;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Invoices.Abstractions;
using ObentoTensho.Core.Invoices.Domain;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Reports.Services;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Invoices.Services;

// Yêu cầu xuất hóa đơn (chủ quán 2026-11-13): thu ngân/khách gửi thông tin người mua kèm đơn; kế toán xem (CCCD che,
// hiện đầy đủ có audit), xuất Excel để lập hóa đơn điện tử trên phần mềm của nhà cung cấp, rồi ghi lại số hóa đơn.
public sealed class InvoiceService(
    IInvoiceRepository invoices,
    IOrderRepository orders,
    IValidator<InvoiceBuyerRequest> validator,
    ITableSpreadsheet spreadsheet,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public const int MaxPageSize = 100;
    public const int ExportLimit = 5000;
    private const string Entity = "invoice_request";

    // Lỗi theo trường, khóa "invoice.taxCode"… để form đặt món/thanh toán hiện đúng ô
    public IReadOnlyDictionary<string, string[]> Validate(InvoiceBuyerRequest request, string prefix = "invoice")
    {
        ArgumentNullException.ThrowIfNull(request);
        return validator.Validate(request).Errors
            .GroupBy(e => $"{prefix}.{char.ToLowerInvariant(e.PropertyName[0])}{e.PropertyName[1..]}", StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorCode).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
    }

    // Gắn yêu cầu vào đơn vừa tạo/vừa thanh toán, trong transaction của bên gọi (đã Validate trước)
    public InvoiceRequest Attach(Guid orderId, InvoiceBuyerRequest request, Guid? staffId)
    {
        ArgumentNullException.ThrowIfNull(request);
        var nationalId = request.BuyerType == InvoiceBuyerType.Individual ? InvoiceRules.Clean(request.NationalId) : null;
        var taxCode = request.BuyerType == InvoiceBuyerType.Company ? InvoiceRules.Clean(request.TaxCode)?.Replace("-", string.Empty, StringComparison.Ordinal) : null;
        VietnamesePhoneNumber.TryNormalize(request.Phone, out var phone);
        var invoice = new InvoiceRequest
        {
            OrderId = orderId,
            BuyerType = request.BuyerType,
            BuyerName = request.BuyerName.Trim(),
            NationalId = nationalId,
            TaxCode = taxCode,
            Address = InvoiceRules.Clean(request.Address),
            Email = InvoiceRules.Clean(request.Email) is { } email ? EmailAddress.Normalize(email) : null,
            Phone = phone,
            Note = InvoiceRules.Clean(request.Note),
            CreatedByStaffId = staffId,
            CreatedAtUtc = clock.UtcNow,
        };
        invoices.Add(invoice);
        return invoice;
    }

    // Thu ngân thêm yêu cầu cho đơn đã có (mang về, hoặc khách tại bàn xin sau khi thanh toán)
    public async Task<Result<InvoiceRequestDto>> CreateForOrderAsync(Guid orderId, InvoiceBuyerRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Validate(request, "invoice") is { Count: > 0 } errors)
        {
            return Result.Failure<InvoiceRequestDto>(ErrorCodes.ValidationFailed, errors);
        }

        if (await orders.FindForUpdateAsync(orderId, withItems: false, cancellationToken) is not { Status: not OrderStatus.Cancelled } order)
        {
            return Result.Failure<InvoiceRequestDto>(ErrorCodes.NotFound);
        }

        if (await invoices.HasActiveForOrderAsync(order.Id, cancellationToken))
        {
            return Result.Failure<InvoiceRequestDto>(ErrorCodes.InvoiceExists);
        }

        var invoice = Attach(order.Id, request, staffId);
        audit.Record(new AuditEntry("invoices.request.created", Entity, Id(invoice), After: new { order.OrderCode, request.BuyerType }));
        return await SaveAsync(invoice, cancellationToken);
    }

    public async Task<PagedResponse<InvoiceRequestDto>> ListAsync(InvoiceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var (from, to) = Range(query);
        var (items, total) = await invoices.ListAsync(query with { Page = page, PageSize = pageSize }, from, to, cancellationToken);
        return new PagedResponse<InvoiceRequestDto>(items.Select(ToDto).ToArray(), new PageMeta(page, pageSize, total));
    }

    public async Task<Result<InvoiceContactDto>> RevealAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await invoices.FindRowAsync(id, cancellationToken) is not { } row)
        {
            return Result.Failure<InvoiceContactDto>(ErrorCodes.NotFound);
        }

        audit.Record(new AuditEntry("invoices.request.contact_revealed", Entity, Id(row.Request)));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new InvoiceContactDto(row.Request.NationalId, row.Request.Phone));
    }

    public async Task<Result<InvoiceRequestDto>> IssueAsync(Guid id, IssueInvoiceRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var number = InvoiceRules.Clean(request.InvoiceNumber);
        if (number is null || number.Length > InvoiceRules.InvoiceNumberMaxLength)
        {
            return Result.Failure<InvoiceRequestDto>(ErrorCodes.ValidationFailed, Field("invoiceNumber", number is null ? FieldErrorCodes.Required : FieldErrorCodes.TooLong));
        }

        if (await invoices.FindForUpdateAsync(id, cancellationToken) is not { } invoice)
        {
            return Result.Failure<InvoiceRequestDto>(ErrorCodes.NotFound);
        }

        if (invoice.Status != InvoiceRequestStatus.Pending)
        {
            return Result.Failure<InvoiceRequestDto>(ErrorCodes.InvalidTransition);
        }

        invoice.Status = InvoiceRequestStatus.Issued;
        invoice.InvoiceNumber = number;
        invoice.IssuedAtUtc = clock.UtcNow;
        invoice.IssuedByStaffId = staffId;
        audit.Record(new AuditEntry("invoices.request.issued", Entity, Id(invoice), After: new { number }));
        return await SaveAsync(invoice, cancellationToken);
    }

    public async Task<Result<InvoiceRequestDto>> CancelAsync(Guid id, CancelInvoiceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = InvoiceRules.Clean(request.Reason);
        if (reason is null || reason.Length > InvoiceRules.NoteMaxLength)
        {
            return Result.Failure<InvoiceRequestDto>(ErrorCodes.ValidationFailed, Field("reason", reason is null ? FieldErrorCodes.Required : FieldErrorCodes.TooLong));
        }

        if (await invoices.FindForUpdateAsync(id, cancellationToken) is not { } invoice)
        {
            return Result.Failure<InvoiceRequestDto>(ErrorCodes.NotFound);
        }

        // Đã xuất thì phải điều chỉnh/thay thế trên phần mềm hóa đơn điện tử, không hủy ở đây
        if (invoice.Status != InvoiceRequestStatus.Pending)
        {
            return Result.Failure<InvoiceRequestDto>(ErrorCodes.InvalidTransition);
        }

        invoice.Status = InvoiceRequestStatus.Cancelled;
        invoice.CancelReason = reason;
        invoice.CancelledAtUtc = clock.UtcNow;
        audit.Record(new AuditEntry("invoices.request.cancelled", Entity, Id(invoice), After: new { reason }));
        return await SaveAsync(invoice, cancellationToken);
    }

    // Tệp cho kế toán: đầy đủ CCCD/SĐT (có audit), tối đa 5.000 dòng mỗi lần
    public async Task<ReportFile> ExportAsync(InvoiceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (from, to) = Range(query);
        var (items, _) = await invoices.ListAsync(query with { Page = 1, PageSize = ExportLimit }, from, to, cancellationToken);
        string[] columns = ["Mã đơn", "Ngày đơn", "Kênh", "Tổng tiền", "Loại", "Tên người mua / công ty", "Số CCCD", "Mã số thuế", "Địa chỉ", "Email", "SĐT", "Ghi chú", "Trạng thái", "Số hóa đơn"];
        var rows = items.Select(r => (IReadOnlyList<string>)new[]
        {
            r.OrderCode,
            clock.ToLocal(r.OrderCreatedAtUtc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            r.Channel == OrderChannel.DineIn ? "Tại quán" : "Mang về",
            r.TotalAmount.ToString("0", CultureInfo.InvariantCulture),
            r.Request.BuyerType == InvoiceBuyerType.Company ? "Doanh nghiệp" : "Cá nhân",
            r.Request.BuyerName,
            r.Request.NationalId ?? string.Empty,
            r.Request.TaxCode ?? string.Empty,
            r.Request.Address ?? string.Empty,
            r.Request.Email ?? string.Empty,
            r.Request.Phone ?? string.Empty,
            r.Request.Note ?? string.Empty,
            StatusLabel(r.Request.Status),
            r.Request.InvoiceNumber ?? string.Empty,
        }.Select(SpreadsheetText.Guard).ToArray()).ToArray();
        audit.Record(new AuditEntry("invoices.request.exported", Entity, "export", After: new { count = rows.Length, query.Status, query.From, query.To }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var name = string.Create(CultureInfo.InvariantCulture, $"yeu-cau-hoa-don-{clock.TodayLocal:yyyy-MM-dd}.xlsx");
        return new ReportFile(name, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", spreadsheet.Write("Yêu cầu hóa đơn", columns, rows));
    }

    private async Task<Result<InvoiceRequestDto>> SaveAsync(InvoiceRequest invoice, CancellationToken cancellationToken)
    {
        var saved = await unitOfWork.TrySaveAsync(ErrorCodes.PreconditionFailed, cancellationToken);
        return saved.IsSuccess && await invoices.FindRowAsync(invoice.Id, cancellationToken) is { } row
            ? Result.Success(ToDto(row))
            : Result.Failure<InvoiceRequestDto>(saved.ErrorCode ?? ErrorCodes.NotFound);
    }

    private (DateTime? From, DateTime? To) Range(InvoiceQuery query) =>
        (query.From is { } from ? clock.ToUtc(from, TimeOnly.MinValue) : null, query.To is { } to ? clock.ToUtc(to.AddDays(1), TimeOnly.MinValue) : null);

    private static InvoiceRequestDto ToDto(InvoiceRow row)
    {
        var r = row.Request;
        return new InvoiceRequestDto(
            r.Id, r.OrderId, row.OrderCode, row.Channel, row.TotalAmount, row.OrderCreatedAtUtc, r.BuyerType, r.BuyerName,
            InvoiceRules.MaskNationalId(r.NationalId), r.TaxCode, r.Address, r.Email, r.Phone is null ? null : PiiMask.Phone(r.Phone),
            r.Note, r.Status, r.InvoiceNumber, r.IssuedAtUtc, r.CancelReason, r.CreatedAtUtc, RowVersions.Encode(r.RowVersion));
    }

    private static string StatusLabel(InvoiceRequestStatus status) => status switch
    {
        InvoiceRequestStatus.Issued => "Đã xuất",
        InvoiceRequestStatus.Cancelled => "Đã hủy",
        _ => "Chờ xuất",
    };

    private static Dictionary<string, string[]> Field(string name, string code) => new(StringComparer.Ordinal) { [name] = [code] };

    private static string Id(InvoiceRequest request) => request.Id.ToString("D", CultureInfo.InvariantCulture);
}
