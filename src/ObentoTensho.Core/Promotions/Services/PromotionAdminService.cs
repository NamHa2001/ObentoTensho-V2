using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Promotions;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Menu.Services;
using ObentoTensho.Core.Promotions.Abstractions;
using ObentoTensho.Core.Promotions.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Promotions.Services;

// docs/srs/14 §3: quản lý chương trình khuyến mãi; mô tả làm sạch trước khi lưu (SEC-08); xóa mềm, slug trùng ⇒ 409 (BR-VCH-17)
public sealed class PromotionAdminService(
    IPromotionRepository promotions,
    IPromotionCache cache,
    IAuditTrail audit,
    IHtmlSanitizer sanitizer,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<PromotionRequest> validator)
{
    public const int MaxPageSize = 100;
    private const int SlugMaxLength = 200;
    private const string Entity = "promotion";

    public async Task<PagedResponse<AdminPromotionDto>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (items, total) = await promotions.ListAsync(page, pageSize, cancellationToken);
        return new PagedResponse<AdminPromotionDto>(items.Select(p => p.ToAdmin()).ToArray(), new PageMeta(page, pageSize, total));
    }

    public async Task<Result<AdminPromotionDto>> GetAsync(int id, CancellationToken cancellationToken) =>
        await promotions.FindAsync(id, cancellationToken) is { } promotion
            ? Result.Success(promotion.ToAdmin())
            : Result.Failure<AdminPromotionDto>(ErrorCodes.NotFound);

    public async Task<Result<AdminPromotionDto>> CreateAsync(PromotionRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<AdminPromotionDto>();
        }

        if (request.Slug is { } requested && await promotions.SlugExistsAsync(requested, null, cancellationToken))
        {
            return Result.Failure<AdminPromotionDto>(ErrorCodes.DuplicateName);
        }

        var slug = await SlugGenerator.UniqueAsync(request.Slug, request.Title, SlugMaxLength, s => promotions.SlugExistsAsync(s, null, cancellationToken));
        var promotion = new Promotion
        {
            Title = request.Title.Trim(),
            Slug = slug,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedByStaffId = staffId,
            CreatedAtUtc = clock.UtcNow,
        };
        Apply(promotion, request);
        var result = await unitOfWork.TryCreateAsync(async ct =>
        {
            promotions.Add(promotion);
            await unitOfWork.SaveChangesAsync(ct);
            audit.Record(new AuditEntry($"promotions.{Entity}.created", Entity, Id(promotion), After: Snapshot(promotion)));
            await unitOfWork.SaveChangesAsync(ct);
            return promotion.ToAdmin();
        }, ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    public async Task<Result<AdminPromotionDto>> UpdateAsync(int id, PromotionRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<AdminPromotionDto>();
        }

        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure<AdminPromotionDto>(ErrorCodes.PreconditionRequired);
        }

        if (await promotions.FindAsync(id, cancellationToken) is not { } promotion)
        {
            return Result.Failure<AdminPromotionDto>(ErrorCodes.NotFound);
        }

        if (request.Slug is { } slug && await promotions.SlugExistsAsync(slug, id, cancellationToken))
        {
            return Result.Failure<AdminPromotionDto>(ErrorCodes.DuplicateName);
        }

        unitOfWork.ExpectRowVersion(promotion, version);
        var before = Snapshot(promotion);
        promotion.Slug = request.Slug ?? promotion.Slug;
        Apply(promotion, request);
        audit.Record(new AuditEntry($"promotions.{Entity}.updated", Entity, Id(promotion), before, Snapshot(promotion)));
        var result = await unitOfWork.TrySaveAsync(promotion.ToAdmin, ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    public async Task<Result> DeleteAsync(int id, string? ifMatch, CancellationToken cancellationToken)
    {
        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure(ErrorCodes.PreconditionRequired);
        }

        if (await promotions.FindAsync(id, cancellationToken) is not { } promotion)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        unitOfWork.ExpectRowVersion(promotion, version);
        promotion.DeletedAtUtc = clock.UtcNow;
        audit.Record(new AuditEntry($"promotions.{Entity}.deleted", Entity, Id(promotion), Before: Snapshot(promotion)));
        var result = await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    private void Apply(Promotion promotion, PromotionRequest request)
    {
        promotion.Title = request.Title.Trim();
        promotion.DescriptionHtml = string.IsNullOrWhiteSpace(request.DescriptionHtml) ? null : sanitizer.Sanitize(request.DescriptionHtml);
        promotion.StartDate = request.StartDate;
        promotion.EndDate = request.EndDate;
        promotion.PublicTeaser = request.PublicTeaser;
        promotion.IsActive = request.IsActive;
        promotion.UpdatedAtUtc = clock.UtcNow;
    }

    private static object Snapshot(Promotion p) => new { p.Title, p.Slug, p.StartDate, p.EndDate, p.PublicTeaser, p.IsActive };

    private static string Id(Promotion promotion) => promotion.Id.ToString(CultureInfo.InvariantCulture);
}
