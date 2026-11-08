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
using ObentoTensho.Core.Localization.Abstractions;
using ObentoTensho.Core.Localization.Services;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Services;
using ObentoTensho.Core.Menu.Services;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Content.Services;

// FR-CNT-03: soạn bài. HTML làm sạch khi lưu (BR-CNT-02); bài đã đăng đổi slug ⇒ lưu chuyển hướng (BR-CNT-41);
// xóa mềm vào thùng rác (BR-CNT-42); hủy xuất bản không xóa ngày đăng (BR-CNT-40)
public sealed class NewsAdminService(
    INewsRepository news,
    ITranslationRepository translations,
    IImageRepository images,
    ImageMapper imageMapper,
    IHtmlSanitizer sanitizer,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<NewsRequest> validator)
{
    public const int MaxPageSize = 50;
    public const int TrashDays = 30;
    private const int SlugMaxLength = 300;
    private const string Entity = "news";

    public async Task<PagedResponse<AdminNewsListItemDto>> ListAsync(NewsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var (items, total) = await news.ListAsync(query with { Page = page, PageSize = pageSize }, cancellationToken);
        return new PagedResponse<AdminNewsListItemDto>(
            items.Select(n => new AdminNewsListItemDto(n.Id, n.Title, n.Slug, n.CategoryId, n.AuthorDisplayName, n.Status, n.PublishedAtUtc, n.DeletedAtUtc, RowVersions.Encode(n.RowVersion))).ToArray(),
            new PageMeta(page, pageSize, total));
    }

    public async Task<Result<AdminNewsDto>> GetAsync(int id, CancellationToken cancellationToken) =>
        await news.FindAsync(id, includeDeleted: false, cancellationToken) is { } article
            ? Result.Success(await ToDtoAsync(article, cancellationToken))
            : Result.Failure<AdminNewsDto>(ErrorCodes.NotFound);

    public async Task<Result<AdminNewsDto>> CreateAsync(NewsRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        if (await CheckAsync(request, cancellationToken) is { } failure)
        {
            return Result.Failure<AdminNewsDto>(failure.ErrorCode ?? ErrorCodes.ValidationFailed, failure.FieldErrors);
        }

        if (request.Slug is { } requested && await news.SlugTakenAsync(requested, null, cancellationToken))
        {
            return Result.Failure<AdminNewsDto>(ErrorCodes.DuplicateName);
        }

        var article = new NewsArticle
        {
            Title = string.Empty,
            Slug = await SlugGenerator.UniqueAsync(request.Slug, request.Title, SlugMaxLength, s => news.SlugTakenAsync(s, null, cancellationToken)),
            TitleSearch = string.Empty,
            ContentHtml = string.Empty,
            AuthorId = staffId,
            CreatedAtUtc = clock.UtcNow,
        };
        Apply(article, request);
        return await unitOfWork.TryCreateAsync(async ct =>
        {
            news.Add(article);
            await unitOfWork.SaveChangesAsync(ct);
            audit.Record(new AuditEntry($"content.{Entity}.created", Entity, Id(article), After: Snapshot(article)));
            await unitOfWork.SaveChangesAsync(ct);
            return await ToDtoAsync(article, ct);
        }, ErrorCodes.DuplicateName, cancellationToken);
    }

    public async Task<Result<AdminNewsDto>> UpdateAsync(int id, NewsRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        if (await CheckAsync(request, cancellationToken) is { } failure)
        {
            return Result.Failure<AdminNewsDto>(failure.ErrorCode ?? ErrorCodes.ValidationFailed, failure.FieldErrors);
        }

        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure<AdminNewsDto>(ErrorCodes.PreconditionRequired);
        }

        if (request.Slug is { } slug && await news.SlugTakenAsync(slug, id, cancellationToken))
        {
            return Result.Failure<AdminNewsDto>(ErrorCodes.DuplicateName);
        }

        return await unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (await news.FindAsync(id, includeDeleted: false, ct) is not { } article)
            {
                return Result.Failure<AdminNewsDto>(ErrorCodes.NotFound);
            }

            unitOfWork.ExpectRowVersion(article, version);
            var before = Snapshot(article);
            await ChangeSlugAsync(article, request.Slug, ct);
            Apply(article, request);
            article.TitleSearch = NewsSearchText.Build(article, await translations.ListForEntityAsync(TranslatableContent.News, article.Id, ct));
            audit.Record(new AuditEntry($"content.{Entity}.updated", Entity, Id(article), before, Snapshot(article)));
            var saved = await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, ct);
            return saved.IsSuccess
                ? Result.Success(await ToDtoAsync(article, ct))
                : Result.Failure<AdminNewsDto>(saved.ErrorCode ?? ErrorCodes.PreconditionFailed);
        }, cancellationToken);
    }

    public async Task<Result> DeleteAsync(int id, string? ifMatch, CancellationToken cancellationToken)
    {
        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure(ErrorCodes.PreconditionRequired);
        }

        if (await news.FindAsync(id, includeDeleted: false, cancellationToken) is not { } article)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        unitOfWork.ExpectRowVersion(article, version);
        article.IsDeleted = true;
        article.DeletedAtUtc = clock.UtcNow;
        audit.Record(new AuditEntry($"content.{Entity}.deleted", Entity, Id(article), Before: Snapshot(article)));
        return await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, cancellationToken);
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken cancellationToken)
    {
        if (await news.FindAsync(id, includeDeleted: true, cancellationToken) is not { IsDeleted: true } article)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        article.IsDeleted = false;
        article.DeletedAtUtc = null;
        audit.Record(new AuditEntry($"content.{Entity}.restored", Entity, Id(article), After: Snapshot(article)));
        return await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, cancellationToken);
    }

    public Task<int> PurgeTrashAsync(CancellationToken cancellationToken) =>
        news.PurgeTrashAsync(clock.UtcNow.AddDays(-TrashDays), cancellationToken);

    private async Task<Result?> CheckAsync(NewsRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request.CategoryId is { } categoryId && await news.FindCategoryAsync(categoryId, cancellationToken) is null)
        {
            errors["categoryId"] = [FieldErrorCodes.NotFound];
        }

        var imageIds = new[] { request.ThumbnailImageId, request.OgImageId }.OfType<Guid>().ToArray();
        var loaded = await images.LoadAsync(imageIds, cancellationToken);
        if (!imageIds.All(loaded.ContainsKey))
        {
            errors["imageId"] = [FieldErrorCodes.NotFound];
        }

        return errors.Count > 0 ? Result.Failure(ErrorCodes.ValidationFailed, errors) : null;
    }

    // BR-CNT-41: bài đã từng công khai đổi slug thì giữ chuyển hướng; đổi về slug cũ thì bỏ chuyển hướng đó
    private async Task ChangeSlugAsync(NewsArticle article, string? slug, CancellationToken cancellationToken)
    {
        if (slug is null || string.Equals(slug, article.Slug, StringComparison.Ordinal))
        {
            return;
        }

        await news.RemoveRedirectAsync(slug, cancellationToken);
        if (article.PublishedAtUtc <= clock.UtcNow)
        {
            news.AddRedirect(new SlugRedirect { EntityType = SlugRedirect.NewsEntity, OldSlug = article.Slug, EntityId = article.Id, CreatedAtUtc = clock.UtcNow });
        }

        article.Slug = slug;
    }

    private void Apply(NewsArticle article, NewsRequest request)
    {
        var now = clock.UtcNow;
        article.Title = request.Title.Trim();
        article.TitleSearch = TextNormalizer.Search(article.Title);
        article.ThumbnailImageId = request.ThumbnailImageId;
        article.Summary = Clean(request.Summary);
        article.ContentHtml = sanitizer.SanitizeRich(request.ContentHtml);
        article.CategoryId = request.CategoryId;
        article.AuthorDisplayName = Clean(request.AuthorDisplayName) ?? NewsArticle.DefaultAuthorName;
        article.MetaTitle = Clean(request.MetaTitle);
        article.MetaDescription = Clean(request.MetaDescription);
        article.OgImageId = request.OgImageId;
        article.Status = request.Status;
        article.PublishedAtUtc = request.PublishedAtUtc ?? article.PublishedAtUtc ?? (request.Status == NewsStatus.Published ? now : null);
        article.UpdatedAtUtc = now;
    }

    private async Task<AdminNewsDto> ToDtoAsync(NewsArticle n, CancellationToken cancellationToken)
    {
        var loaded = await images.LoadAsync(new[] { n.ThumbnailImageId, n.OgImageId }.OfType<Guid>().Distinct().ToArray(), cancellationToken);
        return new AdminNewsDto(
            n.Id,
            n.Title,
            n.Slug,
            n.ThumbnailImageId is { } t && loaded.TryGetValue(t, out var thumb) ? imageMapper.ToDto(thumb) : null,
            n.Summary,
            n.ContentHtml,
            n.CategoryId,
            n.AuthorDisplayName,
            n.MetaTitle,
            n.MetaDescription,
            n.OgImageId is { } o && loaded.TryGetValue(o, out var og) ? imageMapper.ToDto(og) : null,
            n.Status,
            n.PublishedAtUtc,
            RowVersions.Encode(n.RowVersion));
    }

    private static object Snapshot(NewsArticle n) => new { n.Title, n.Slug, n.Status, n.PublishedAtUtc, n.CategoryId };

    private static string Id(NewsArticle article) => article.Id.ToString(CultureInfo.InvariantCulture);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
