using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Content.Services;

// FR-CNT-05: khách đánh giá món trong đơn hoàn thành của chính mình; công khai chỉ đánh giá đã duyệt
public sealed class ReviewService(
    IReviewRepository reviews,
    IUnitOfWork unitOfWork,
    IClock clock,
    IAuditTrail audit,
    IValidator<CreateReviewRequest> createValidator,
    IValidator<UpdateReviewRequest> updateValidator)
{
    public const int MaxPageSize = 50;
    private const string Entity = "review";

    public async Task<PagedResponse<PublicReviewDto>> ListPublicAsync(int? menuItemId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (items, total) = await reviews.ListAsync(ReviewStatus.Approved, menuItemId, page, pageSize, cancellationToken);
        return new PagedResponse<PublicReviewDto>(
            items.Select(row => new PublicReviewDto(
                row.Review.Id,
                row.Review.MenuItemId,
                Review.ShortAuthorName(row.FullName),
                row.Review.Rating,
                row.Review.Comment,
                row.Review.ReplyContent,
                row.Review.RepliedAtUtc,
                row.Review.CreatedAtUtc)).ToArray(),
            new PageMeta(page, pageSize, total));
    }

    public async Task<IReadOnlyList<MyReviewDto>> ListMineAsync(Guid userId, CancellationToken cancellationToken) =>
        (await reviews.ListMineAsync(userId, cancellationToken)).Select(ToDto).ToArray();

    public async Task<Result<MyReviewDto>> CreateAsync(Guid userId, CreateReviewRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<MyReviewDto>();
        }

        if (await reviews.FindOrderAsync(request.OrderId, userId, cancellationToken) is not { } order)
        {
            return Result.Failure<MyReviewDto>(ErrorCodes.NotFound);
        }

        var now = clock.UtcNow;
        var open = order.Status == OrderStatus.Completed && order.CompletedAtUtc is { } completed && now <= completed.AddDays(Review.WindowDays);
        var inOrder = request.MenuItemId is not { } itemId || order.MenuItemIds.Contains(itemId);
        if (!open || !inOrder)
        {
            return Result.Failure<MyReviewDto>(ErrorCodes.ReviewNotAllowed);
        }

        if (await reviews.CountCreatedSinceAsync(userId, clock.ToUtc(clock.TodayLocal, TimeOnly.MinValue), cancellationToken) >= Review.MaxPerDay)
        {
            return Result.Failure<MyReviewDto>(ErrorCodes.RateLimited);
        }

        if (await reviews.ExistsAsync(userId, request.OrderId, request.MenuItemId, cancellationToken))
        {
            return Result.Failure<MyReviewDto>(ErrorCodes.ReviewExists);
        }

        var review = new Review
        {
            UserId = userId,
            OrderId = request.OrderId,
            MenuItemId = request.MenuItemId,
            Rating = (byte)request.Rating,
            Comment = Clean(request.Comment),
            CreatedAtUtc = now,
        };
        return await unitOfWork.TryCreateAsync(async ct =>
        {
            reviews.Add(review);
            await unitOfWork.SaveChangesAsync(ct);
            return ToDto(review);
        }, ErrorCodes.ReviewExists, cancellationToken);
    }

    public async Task<Result<MyReviewDto>> UpdateAsync(Guid userId, int id, UpdateReviewRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<MyReviewDto>();
        }

        if (await reviews.FindAsync(id, cancellationToken) is not { } review || review.UserId != userId)
        {
            return Result.Failure<MyReviewDto>(ErrorCodes.NotFound);
        }

        if (!review.CanEdit(clock.UtcNow))
        {
            return Result.Failure<MyReviewDto>(ErrorCodes.ReviewLocked);
        }

        review.Rating = (byte)request.Rating;
        review.Comment = Clean(request.Comment);

        // BR-CNT-16: nội dung đã đổi thì phải duyệt lại
        review.Status = ReviewStatus.Pending;
        review.ReviewedAtUtc = null;
        review.ReviewedByStaffId = null;
        return await unitOfWork.TrySaveAsync(() => ToDto(review), ErrorCodes.ReviewExists, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid userId, int id, CancellationToken cancellationToken)
    {
        if (await reviews.FindAsync(id, cancellationToken) is not { } review || review.UserId != userId)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        if (!review.CanEdit(clock.UtcNow))
        {
            return Result.Failure(ErrorCodes.ReviewLocked);
        }

        audit.Record(new AuditEntry($"content.{Entity}.deleted", Entity, review.Id.ToString(CultureInfo.InvariantCulture), Before: new { review.OrderId, review.MenuItemId, review.Rating }));
        reviews.Remove(review);
        return await unitOfWork.TrySaveAsync(ErrorCodes.ReviewExists, cancellationToken);
    }

    private MyReviewDto ToDto(Review r) =>
        new(r.Id, r.OrderId, r.MenuItemId, r.Rating, r.Comment, r.Status, r.ReplyContent, r.CreatedAtUtc, r.CanEdit(clock.UtcNow));

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
