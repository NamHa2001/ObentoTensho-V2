using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Localization.Services;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Content.Services;

// FR-CNT-04: trang Giới thiệu, Liên hệ, pháp lý. Slug theo danh sách cho phép; mỗi lần lưu là một phiên bản, khôi phục tạo phiên bản mới
public sealed class StaticPageService(
    IStaticPageRepository pages,
    IHtmlSanitizer sanitizer,
    ContentLocalizer localizer,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<StaticPageRequest> validator)
{
    private const string Entity = "static_page";

    public async Task<IReadOnlyList<StaticPageSummaryDto>> ListAsync(CancellationToken cancellationToken)
    {
        var saved = (await pages.ListAsync(cancellationToken)).ToDictionary(p => p.Slug, StringComparer.Ordinal);
        return StaticPage.AllowedSlugs
            .Select(slug => saved.TryGetValue(slug, out var page)
                ? new StaticPageSummaryDto(slug, page.Title, page.Version, page.UpdatedAtUtc)
                : new StaticPageSummaryDto(slug, null, 0, null))
            .ToArray();
    }

    // Trang công khai và trang quản trị đọc cùng một nội dung; trang chưa có nội dung ⇒ 404
    public async Task<Result<StaticPageDto>> GetAsync(string slug, CancellationToken cancellationToken) =>
        IsAllowed(slug) && await pages.FindAsync(slug, cancellationToken) is { } page
            ? Result.Success(ToDto(page))
            : Result.Failure<StaticPageDto>(ErrorCodes.NotFound);

    // Trang cho khách theo ngôn ngữ; bản dịch thiếu hoặc lỗi thời ⇒ tiếng Việt (BR-I18N-07)
    public async Task<Result<PublicStaticPageDto>> GetPublicAsync(string slug, string? lang, CancellationToken cancellationToken)
    {
        if (!IsAllowed(slug) || await pages.FindAsync(slug, cancellationToken) is not { } page)
        {
            return Result.Failure<PublicStaticPageDto>(ErrorCodes.NotFound);
        }

        var text = (await localizer.ForAsync(TranslatableContent.StaticPage, lang, cancellationToken)).Apply(page.Id, ContentSources.Of(page));
        return Result.Success(new PublicStaticPageDto(page.Slug, text.Text.Title ?? page.Title, text.Text.BodyHtml ?? page.ContentHtml, page.UpdatedAtUtc, text.ContentLang, text.IsFallback));
    }

    public async Task<Result<IReadOnlyList<StaticPageRevisionDto>>> RevisionsAsync(string slug, CancellationToken cancellationToken)
    {
        if (!IsAllowed(slug) || await pages.FindAsync(slug, cancellationToken) is not { } page)
        {
            return Result.Failure<IReadOnlyList<StaticPageRevisionDto>>(ErrorCodes.NotFound);
        }

        return Result.Success<IReadOnlyList<StaticPageRevisionDto>>(
            (await pages.ListVersionsAsync(page.Id, cancellationToken)).Select(v => new StaticPageRevisionDto(v.Version, v.Title, v.UpdatedByStaffId, v.UpdatedAtUtc)).ToArray());
    }

    public async Task<Result<StaticPageDto>> SaveAsync(string slug, StaticPageRequest request, string? ifMatch, Guid staffId, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<StaticPageDto>();
        }

        if (!IsAllowed(slug))
        {
            return Result.Failure<StaticPageDto>(ErrorCodes.NotFound);
        }

        return await WriteAsync(slug, request.Title.Trim(), sanitizer.SanitizeRich(request.ContentHtml), ifMatch, staffId, cancellationToken);
    }

    public async Task<Result<StaticPageDto>> RestoreAsync(string slug, int revision, string? ifMatch, Guid staffId, CancellationToken cancellationToken)
    {
        if (!IsAllowed(slug) || await pages.FindAsync(slug, cancellationToken) is not { } page
            || await pages.FindVersionAsync(page.Id, revision, cancellationToken) is not { } version)
        {
            return Result.Failure<StaticPageDto>(ErrorCodes.NotFound);
        }

        return await WriteAsync(slug, version.Title, version.ContentHtml, ifMatch, staffId, cancellationToken);
    }

    private Task<Result<StaticPageDto>> WriteAsync(string slug, string title, string html, string? ifMatch, Guid staffId, CancellationToken cancellationToken) =>
        unitOfWork.TryInTransactionAsync(async ct =>
        {
            var now = clock.UtcNow;
            var page = await pages.FindAsync(slug, ct);
            if (page is null)
            {
                page = new StaticPage { Slug = slug, Title = title, ContentHtml = html, UpdatedByStaffId = staffId, UpdatedAtUtc = now };
                pages.Add(page);
            }
            else
            {
                if (RowVersions.Decode(ifMatch) is not { } version)
                {
                    return Result.Failure<StaticPageDto>(ErrorCodes.PreconditionRequired);
                }

                unitOfWork.ExpectRowVersion(page, version);
                page.Title = title;
                page.ContentHtml = html;
                page.UpdatedByStaffId = staffId;
                page.UpdatedAtUtc = now;
            }

            page.Version++;
            var saved = await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, ct);
            if (!saved.IsSuccess)
            {
                return Result.Failure<StaticPageDto>(saved.ErrorCode ?? ErrorCodes.PreconditionFailed);
            }

            pages.AddVersion(new StaticPageVersion { PageId = page.Id, Version = page.Version, Title = title, ContentHtml = html, UpdatedByStaffId = staffId, UpdatedAtUtc = now });
            audit.Record(new AuditEntry($"content.{Entity}.saved", Entity, page.Id.ToString(CultureInfo.InvariantCulture), After: new { page.Slug, page.Version }));
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(ToDto(page));
        }, cancellationToken);

    private static bool IsAllowed(string slug) => StaticPage.AllowedSlugs.Contains(slug, StringComparer.Ordinal);

    private static StaticPageDto ToDto(StaticPage p) =>
        new(p.Slug, p.Title, p.ContentHtml, p.Version, p.UpdatedAtUtc, RowVersions.Encode(p.RowVersion));
}
