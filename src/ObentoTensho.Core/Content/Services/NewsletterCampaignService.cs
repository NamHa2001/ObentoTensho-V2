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

// Soạn và xếp lịch bản tin; nội dung làm sạch khi lưu (BR-CNT-24); request chỉ đổi trạng thái, không gửi (BR-CNT-23)
public sealed class NewsletterCampaignService(
    INewsletterRepository newsletter,
    IHtmlSanitizer sanitizer,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<NewsletterCampaignRequest> validator)
{
    public const int MaxPageSize = 50;
    private const string Entity = "newsletter_campaign";

    public async Task<PagedResponse<NewsletterCampaignDto>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (items, total) = await newsletter.ListCampaignsAsync(page, pageSize, cancellationToken);
        var rows = new List<NewsletterCampaignDto>(items.Count);
        foreach (var campaign in items)
        {
            rows.Add(await ToDtoAsync(campaign, cancellationToken));
        }

        return new PagedResponse<NewsletterCampaignDto>(rows, new PageMeta(page, pageSize, total));
    }

    public async Task<Result<NewsletterCampaignDto>> GetAsync(int id, CancellationToken cancellationToken) =>
        await newsletter.FindCampaignAsync(id, cancellationToken) is { } campaign
            ? Result.Success(await ToDtoAsync(campaign, cancellationToken))
            : Result.Failure<NewsletterCampaignDto>(ErrorCodes.NotFound);

    public async Task<Result<NewsletterCampaignDto>> CreateAsync(NewsletterCampaignRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<NewsletterCampaignDto>();
        }

        var campaign = new NewsletterCampaign { Subject = string.Empty, ContentHtml = string.Empty, CreatedAtUtc = clock.UtcNow };
        Apply(campaign, request);
        return await unitOfWork.TryCreateAsync(async ct =>
        {
            newsletter.Add(campaign);
            await unitOfWork.SaveChangesAsync(ct);
            audit.Record(new AuditEntry($"content.{Entity}.created", Entity, Id(campaign), After: new { campaign.Subject }));
            await unitOfWork.SaveChangesAsync(ct);
            return await ToDtoAsync(campaign, ct);
        }, ErrorCodes.DuplicateName, cancellationToken);
    }

    public async Task<Result<NewsletterCampaignDto>> UpdateAsync(int id, NewsletterCampaignRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<NewsletterCampaignDto>();
        }

        var found = await FindDraftAsync(id, ifMatch, cancellationToken);
        if (found.ErrorCode is { } error)
        {
            return Result.Failure<NewsletterCampaignDto>(error);
        }

        Apply(found.Campaign!, request);
        var saved = await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, cancellationToken);
        return saved.IsSuccess
            ? Result.Success(await ToDtoAsync(found.Campaign!, cancellationToken))
            : Result.Failure<NewsletterCampaignDto>(saved.ErrorCode ?? ErrorCodes.PreconditionFailed);
    }

    public async Task<Result> DeleteAsync(int id, string? ifMatch, CancellationToken cancellationToken)
    {
        var found = await FindDraftAsync(id, ifMatch, cancellationToken);
        if (found.ErrorCode is { } error)
        {
            return Result.Failure(error);
        }

        newsletter.Remove(found.Campaign!);
        return await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, cancellationToken);
    }

    public async Task<Result> ScheduleAsync(int id, string? ifMatch, Guid staffId, CancellationToken cancellationToken)
    {
        var found = await FindDraftAsync(id, ifMatch, cancellationToken);
        if (found.ErrorCode is { } error)
        {
            return Result.Failure(error);
        }

        var campaign = found.Campaign!;
        campaign.Status = NewsletterCampaignStatus.Scheduled;
        campaign.ScheduledAtUtc ??= clock.UtcNow;
        campaign.SentByStaffId = staffId;
        audit.Record(new AuditEntry($"content.{Entity}.scheduled", Entity, Id(campaign), After: new { campaign.Subject, campaign.ScheduledAtUtc }));
        return await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, cancellationToken);
    }

    private async Task<(NewsletterCampaign? Campaign, string? ErrorCode)> FindDraftAsync(int id, string? ifMatch, CancellationToken cancellationToken)
    {
        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return (null, ErrorCodes.PreconditionRequired);
        }

        if (await newsletter.FindCampaignAsync(id, cancellationToken) is not { } campaign)
        {
            return (null, ErrorCodes.NotFound);
        }

        if (campaign.Status != NewsletterCampaignStatus.Draft)
        {
            return (null, ErrorCodes.InvalidTransition);
        }

        unitOfWork.ExpectRowVersion(campaign, version);
        return (campaign, null);
    }

    private void Apply(NewsletterCampaign campaign, NewsletterCampaignRequest request)
    {
        campaign.Subject = request.Subject.Trim();
        campaign.ContentHtml = sanitizer.SanitizeRich(request.ContentHtml);
        campaign.ScheduledAtUtc = request.ScheduledAtUtc;
    }

    private async Task<NewsletterCampaignDto> ToDtoAsync(NewsletterCampaign c, CancellationToken cancellationToken)
    {
        var (sent, failed) = await newsletter.CountResultsAsync(c.Id, cancellationToken);
        return new NewsletterCampaignDto(c.Id, c.Subject, c.ContentHtml, c.Status, c.ScheduledAtUtc, c.StartedAtUtc, c.FinishedAtUtc, c.RecipientCount, sent, failed, RowVersions.Encode(c.RowVersion));
    }

    private static string Id(NewsletterCampaign campaign) => campaign.Id.ToString(CultureInfo.InvariantCulture);
}
