using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Vouchers;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Promotions.Abstractions;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Vouchers.Abstractions;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Core.Vouchers.Services;

// docs/srs/14 §3: quản lý tạo/sửa/tắt voucher, sinh hàng loạt (BR-VCH-10), duyệt bốn mắt (BR-VCH-21), chặn xóa khi đã dùng (BR-VCH-11)
public sealed class VoucherAdminService(
    IVoucherRepository vouchers,
    IVoucherSettings settings,
    IVoucherSpreadsheet spreadsheet,
    IPromotionCache promotionCache,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<VoucherRequest> validator,
    IValidator<BulkGenerateVouchersRequest> bulkValidator)
{
    public const int MaxPageSize = 100;
    private const string Entity = "voucher";

    // Trần một lần xuất: đủ cho vài lô 5.000 mã mà không nạp cả bảng vào bộ nhớ
    private const int ExportLimit = 20_000;

    private static readonly string[] ExportColumns =
    [
        "Mã", "Loại", "Giá trị", "Giảm tối đa", "Đơn tối thiểu", "Tổng lượt", "Lượt mỗi khách", "Đã dùng",
        "Từ ngày", "Đến ngày", "Đang bật", "Duyệt", "Nguồn",
    ];

    public async Task<PagedResponse<AdminVoucherDto>> ListAsync(VoucherQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var (items, total) = await vouchers.ListAsync(query with { Page = page, PageSize = pageSize }, cancellationToken);
        return new PagedResponse<AdminVoucherDto>(items.Select(v => v.ToAdmin(clock)).ToArray(), new PageMeta(page, pageSize, total));
    }

    // BR-VCH-15: danh sách mã là dữ liệu nhạy cảm nên mỗi lần xuất đều ghi audit
    public async Task<byte[]> ExportAsync(VoucherQuery query, CancellationToken cancellationToken)
    {
        var items = await vouchers.ListForExportAsync(query, ExportLimit, cancellationToken);
        audit.Record(new AuditEntry($"vouchers.{Entity}.exported", Entity, "export", After: new { items.Count, query.Source, query.ApprovalStatus, query.BatchId }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return spreadsheet.Write(ExportColumns, items.Select(ToRow).ToArray());
    }

    public string ExportFileName() =>
        string.Create(CultureInfo.InvariantCulture, $"voucher-{clock.TodayLocal:yyyy-MM-dd}.xlsx");

    public async Task<Result<AdminVoucherDto>> GetAsync(int id, CancellationToken cancellationToken) =>
        await vouchers.FindAsync(id, cancellationToken) is { } voucher
            ? Result.Success(voucher.ToAdmin(clock))
            : Result.Failure<AdminVoucherDto>(ErrorCodes.NotFound);

    public async Task<Result<AdminVoucherDto>> CreateAsync(VoucherRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<AdminVoucherDto>();
        }

        if (await CheckPromotionAsync(request.PromotionId, cancellationToken) is { } missing)
        {
            return Result.Failure<AdminVoucherDto>(ErrorCodes.ValidationFailed, missing);
        }

        var code = request.Code is null ? await UniqueCodeAsync(prefix: null, cancellationToken) : VoucherCodes.Normalize(request.Code);
        if ((await vouchers.ExistingCodesAsync([code], cancellationToken)).Count > 0)
        {
            return Result.Failure<AdminVoucherDto>(ErrorCodes.DuplicateName);
        }

        var voucher = New(code, request, staffId, batchId: null);
        voucher.ApprovalStatus = VoucherRules.NeedsApproval(request.DiscountType, request.DiscountValue, request.MaxUsageTotal, settings)
            ? ApprovalStatus.Pending
            : ApprovalStatus.Approved;
        promotionCache.Invalidate();
        return await unitOfWork.TryCreateAsync(async ct =>
        {
            vouchers.Add(voucher);
            await unitOfWork.SaveChangesAsync(ct);
            audit.Record(new AuditEntry($"vouchers.{Entity}.created", Entity, Id(voucher), After: Snapshot(voucher)));
            await unitOfWork.SaveChangesAsync(ct);
            return voucher.ToAdmin(clock);
        }, ErrorCodes.DuplicateName, cancellationToken);
    }

    // Mã không đổi được; đổi mức giảm/lượt thành voucher cần duyệt ⇒ quay lại chờ duyệt
    public async Task<Result<AdminVoucherDto>> UpdateAsync(int id, VoucherRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<AdminVoucherDto>();
        }

        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure<AdminVoucherDto>(ErrorCodes.PreconditionRequired);
        }

        if (await CheckPromotionAsync(request.PromotionId, cancellationToken) is { } missing)
        {
            return Result.Failure<AdminVoucherDto>(ErrorCodes.ValidationFailed, missing);
        }

        if (await vouchers.FindAsync(id, cancellationToken) is not { } voucher)
        {
            return Result.Failure<AdminVoucherDto>(ErrorCodes.NotFound);
        }

        if (voucher.Source != VoucherSource.Admin)
        {
            return Result.Failure<AdminVoucherDto>(ErrorCodes.InvalidTransition);
        }

        unitOfWork.ExpectRowVersion(voucher, version);
        var before = Snapshot(voucher);
        var termsChanged = voucher.DiscountType != request.DiscountType || voucher.DiscountValue != request.DiscountValue || voucher.MaxUsageTotal != request.MaxUsageTotal;
        Apply(voucher, request);
        if (termsChanged && VoucherRules.NeedsApproval(request.DiscountType, request.DiscountValue, request.MaxUsageTotal, settings))
        {
            voucher.ApprovalStatus = ApprovalStatus.Pending;
            voucher.ReviewedByStaffId = null;
            voucher.ReviewedAtUtc = null;
        }

        audit.Record(new AuditEntry($"vouchers.{Entity}.updated", Entity, Id(voucher), before, Snapshot(voucher)));
        var result = await unitOfWork.TrySaveAsync(() => voucher.ToAdmin(clock), ErrorCodes.DuplicateName, cancellationToken);
        promotionCache.Invalidate();
        return result;
    }

    // BR-VCH-11: đã có lượt dùng thì chỉ được tắt
    public async Task<Result> DeleteAsync(int id, string? ifMatch, CancellationToken cancellationToken)
    {
        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure(ErrorCodes.PreconditionRequired);
        }

        if (await vouchers.FindAsync(id, cancellationToken) is not { } voucher)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        if (voucher.UsedCount > 0 || await vouchers.HasUsageAsync(id, cancellationToken))
        {
            return Result.Failure(ErrorCodes.VoucherInUse);
        }

        unitOfWork.ExpectRowVersion(voucher, version);
        audit.Record(new AuditEntry($"vouchers.{Entity}.deleted", Entity, Id(voucher), Before: Snapshot(voucher)));
        vouchers.Remove(voucher);
        var result = await unitOfWork.TrySaveAsync(ErrorCodes.VoucherInUse, cancellationToken);
        promotionCache.Invalidate();
        return result;
    }

    public async Task<Result<BulkGenerateResultDto>> BulkGenerateAsync(BulkGenerateVouchersRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        var validation = await bulkValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<BulkGenerateResultDto>();
        }

        if (await CheckPromotionAsync(request.Template.PromotionId, cancellationToken) is { } missing)
        {
            return Result.Failure<BulkGenerateResultDto>(ErrorCodes.ValidationFailed, missing);
        }

        var codes = await UniqueCodesAsync(request.Prefix, request.Quantity, cancellationToken);
        var batchId = Guid.CreateVersion7();
        var template = request.Template;
        var status = request.Quantity > settings.BulkApprovalQuantity || VoucherRules.NeedsApproval(template.DiscountType, template.DiscountValue, template.MaxUsageTotal, settings)
            ? ApprovalStatus.Pending
            : ApprovalStatus.Approved;
        var created = codes.Select(code =>
        {
            var voucher = New(code, template, staffId, batchId);
            voucher.ApprovalStatus = status;
            return voucher;
        }).ToArray();

        return await unitOfWork.TryCreateAsync(async ct =>
        {
            vouchers.AddRange(created);
            audit.Record(new AuditEntry($"vouchers.{Entity}.bulk_generated", Entity, batchId.ToString("D", CultureInfo.InvariantCulture), After: new { request.Quantity, request.Prefix, Template = template, status }));
            await unitOfWork.SaveChangesAsync(ct);
            return new BulkGenerateResultDto(batchId, codes.Count, status, codes);
        }, ErrorCodes.DuplicateName, cancellationToken);
    }

    public Task<Result<AdminVoucherDto>> ReviewAsync(int id, Guid staffId, bool approve, string? note, CancellationToken cancellationToken) =>
        unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (await vouchers.FindAsync(id, ct) is not { } voucher)
            {
                return Result.Failure<AdminVoucherDto>(ErrorCodes.NotFound);
            }

            var failure = Review([voucher], staffId, approve);
            if (failure is not null)
            {
                return Result.Failure<AdminVoucherDto>(failure);
            }

            audit.Record(new AuditEntry($"vouchers.{Entity}.{(approve ? "approved" : "rejected")}", Entity, Id(voucher), After: new { note }));
            await unitOfWork.SaveChangesAsync(ct);
            promotionCache.Invalidate();
            return Result.Success(voucher.ToAdmin(clock));
        }, cancellationToken);

    public Task<Result<int>> ReviewBatchAsync(Guid batchId, Guid staffId, bool approve, string? note, CancellationToken cancellationToken) =>
        unitOfWork.TryInTransactionAsync(async ct =>
        {
            var batch = await vouchers.FindBatchAsync(batchId, ct);
            if (batch.Count == 0)
            {
                return Result.Failure<int>(ErrorCodes.NotFound);
            }

            var failure = Review(batch, staffId, approve);
            if (failure is not null)
            {
                return Result.Failure<int>(failure);
            }

            audit.Record(new AuditEntry($"vouchers.batch.{(approve ? "approved" : "rejected")}", Entity, batchId.ToString("D", CultureInfo.InvariantCulture), After: new { batch.Count, note }));
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(batch.Count);
        }, cancellationToken);

    // Nguyên tắc bốn mắt: người duyệt khác người tạo; chỉ duyệt được voucher đang chờ
    private string? Review(IReadOnlyList<Voucher> items, Guid staffId, bool approve)
    {
        if (items.Any(v => v.ApprovalStatus != ApprovalStatus.Pending))
        {
            return ErrorCodes.InvalidTransition;
        }

        if (items.Any(v => v.CreatedByStaffId == staffId))
        {
            return ErrorCodes.FourEyesRequired;
        }

        var now = clock.UtcNow;
        foreach (var voucher in items)
        {
            voucher.ApprovalStatus = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
            voucher.IsActive = approve && voucher.IsActive;
            voucher.ReviewedByStaffId = staffId;
            voucher.ReviewedAtUtc = now;
        }

        return null;
    }

    private async Task<Dictionary<string, string[]>?> CheckPromotionAsync(int? promotionId, CancellationToken cancellationToken) =>
        promotionId is { } id && !await vouchers.PromotionExistsAsync(id, cancellationToken)
            ? new Dictionary<string, string[]>(StringComparer.Ordinal) { ["promotionId"] = [FieldErrorCodes.NotFound] }
            : null;

    private Voucher New(string code, VoucherRequest request, Guid staffId, Guid? batchId)
    {
        var voucher = new Voucher
        {
            Code = code,
            Source = VoucherSource.Admin,
            CreatedByStaffId = staffId,
            BatchId = batchId,
            CreatedAtUtc = clock.UtcNow,
        };
        Apply(voucher, request);
        return voucher;
    }

    private void Apply(Voucher voucher, VoucherRequest request)
    {
        voucher.DiscountType = request.DiscountType;
        voucher.DiscountValue = request.DiscountValue;
        voucher.MaxDiscountAmount = request.DiscountType == DiscountType.Percent ? request.MaxDiscountAmount : null;
        voucher.MinOrderValue = request.MinOrderValue;
        voucher.MaxUsageTotal = request.MaxUsageTotal;
        voucher.MaxUsagePerUser = request.MaxUsagePerUser;
        voucher.ApplicableTier = request.ApplicableTier;
        voucher.RequireLogin = request.RequireLogin;
        voucher.StartUtc = request.StartDate is { } start ? clock.ToUtc(start, TimeOnly.MinValue) : null;
        voucher.EndUtc = request.EndDate is { } end ? clock.ToUtc(end.AddDays(1), TimeOnly.MinValue) : null;
        voucher.IsActive = request.IsActive;
        voucher.PromotionId = request.PromotionId;
        voucher.UpdatedAtUtc = clock.UtcNow;
    }

    private object?[] ToRow(Voucher v) =>
    [
        v.Code, v.DiscountType == DiscountType.Percent ? "Phần trăm" : "Số tiền", v.DiscountValue, v.MaxDiscountAmount, v.MinOrderValue,
        v.MaxUsageTotal, v.MaxUsagePerUser, v.UsedCount,
        VoucherMappings.StartDate(v, clock)?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        VoucherMappings.EndDate(v, clock)?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        v.IsActive ? "Có" : "Không", ApprovalText(v.ApprovalStatus), SourceText(v.Source),
    ];

    private static string ApprovalText(ApprovalStatus status) => status switch
    {
        ApprovalStatus.Approved => "Đã duyệt",
        ApprovalStatus.Rejected => "Từ chối",
        _ => "Chờ duyệt",
    };

    private static string SourceText(VoucherSource source) => source switch
    {
        VoucherSource.Redeem => "Đổi điểm",
        VoucherSource.Birthday => "Sinh nhật",
        VoucherSource.Game => "Game",
        _ => "Quản trị",
    };

    private async Task<string> UniqueCodeAsync(string? prefix, CancellationToken cancellationToken) =>
        (await UniqueCodesAsync(prefix, 1, cancellationToken))[0];

    // Sinh dư rồi bỏ mã trùng (trong lô và với CSDL) cho tới khi đủ — không lỗi 500 khi trùng (BR-VCH-10)
    private async Task<IReadOnlyList<string>> UniqueCodesAsync(string? prefix, int quantity, CancellationToken cancellationToken)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        while (codes.Count < quantity)
        {
            var batch = Enumerable.Range(0, quantity - codes.Count).Select(_ => VoucherCodes.New(prefix)).Where(c => !codes.Contains(c)).Distinct(StringComparer.Ordinal).ToArray();
            var taken = await vouchers.ExistingCodesAsync(batch, cancellationToken);
            codes.UnionWith(batch.Where(c => !taken.Contains(c)));
        }

        return codes.ToArray();
    }

    private static object Snapshot(Voucher v) =>
        new { v.Code, v.DiscountType, v.DiscountValue, v.MaxDiscountAmount, v.MinOrderValue, v.MaxUsageTotal, v.MaxUsagePerUser, v.ApplicableTier, v.RequireLogin, v.StartUtc, v.EndUtc, v.IsActive, v.ApprovalStatus };

    private static string Id(Voucher voucher) => voucher.Id.ToString(CultureInfo.InvariantCulture);
}
