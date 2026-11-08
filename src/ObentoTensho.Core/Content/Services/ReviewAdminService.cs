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

// BR-CNT-13: duyệt, từ chối, trả lời. If-Match để không duyệt nhầm nội dung khách vừa sửa
public sealed class ReviewAdminService(
    IReviewRepository reviews,
    IPublicReviewCache publicCache,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<ReviewReplyRequest> replyValidator)
{
    public const int MaxPageSize = 50;
    private const string Entity = "review";

    public async Task<PagedResponse<AdminReviewDto>> ListAsync(ReviewStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (items, total) = await reviews.ListAsync(status, null, page, pageSize, cancellationToken);
        return new PagedResponse<AdminReviewDto>(
            items.Select(row => new AdminReviewDto(
                row.Review.Id,
                row.FullName,
                row.OrderCode,
                row.Review.MenuItemId,
                row.ItemName,
                row.Review.Rating,
                row.Review.Comment,
                row.Review.Status,
                row.Review.ReplyContent,
                row.Review.RepliedAtUtc,
                row.Review.CreatedAtUtc,
                RowVersions.Encode(row.Review.RowVersion))).ToArray(),
            new PageMeta(page, pageSize, total));
    }

    public Task<Result> ApproveAsync(int id, string? ifMatch, Guid staffId, CancellationToken cancellationToken) =>
        DecideAsync(id, ReviewStatus.Approved, ifMatch, staffId, cancellationToken);

    public Task<Result> RejectAsync(int id, string? ifMatch, Guid staffId, CancellationToken cancellationToken) =>
        DecideAsync(id, ReviewStatus.Rejected, ifMatch, staffId, cancellationToken);

    public async Task<Result> ReplyAsync(int id, ReviewReplyRequest request, string? ifMatch, Guid staffId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await replyValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        return await WithReviewAsync(id, ifMatch, review =>
        {
            review.ReplyContent = request.Content.Trim();
            review.RepliedAtUtc = clock.UtcNow;
            review.RepliedByStaffId = staffId;
            audit.Record(new AuditEntry($"content.{Entity}.replied", Entity, Id(review), After: new { review.ReplyContent }));
        }, cancellationToken);
    }

    private Task<Result> DecideAsync(int id, ReviewStatus status, string? ifMatch, Guid staffId, CancellationToken cancellationToken) =>
        WithReviewAsync(id, ifMatch, review =>
        {
            var before = review.Status;
            review.Status = status;
            review.ReviewedAtUtc = clock.UtcNow;
            review.ReviewedByStaffId = staffId;
            audit.Record(new AuditEntry($"content.{Entity}.decided", Entity, Id(review), new { Status = before }, new { Status = status }));
        }, cancellationToken);

    private async Task<Result> WithReviewAsync(int id, string? ifMatch, Action<Review> change, CancellationToken cancellationToken)
    {
        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure(ErrorCodes.PreconditionRequired);
        }

        if (await reviews.FindAsync(id, cancellationToken) is not { } review)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        unitOfWork.ExpectRowVersion(review, version);
        change(review);
        var saved = await unitOfWork.TrySaveAsync(ErrorCodes.ReviewExists, cancellationToken);
        publicCache.Invalidate();
        return saved;
    }

    private static string Id(Review review) => review.Id.ToString(CultureInfo.InvariantCulture);
}
