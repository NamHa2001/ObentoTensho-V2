using ObentoTensho.Contracts.Dtos.Localization;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Localization.Abstractions;
using ObentoTensho.Core.Localization.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Localization.Services;

// docs/srs/22 §4.2 "Ghi": PUT bản dịch en/ja; SourceHash tính lại từ bản Việt hiện hành khi lưu.
// Trường bản Việt không có thì bản dịch cũng bỏ trống; HTML qua cùng bộ lọc với bản Việt (SEC-08)
public sealed class ContentTranslationService(
    ContentSources sources,
    INewsRepository news,
    ITranslationRepository translations,
    ITranslationCache cache,
    IHtmlSanitizer sanitizer,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<IReadOnlyList<ContentTranslationDto>>> ListAsync(TranslatableContent kind, string key, CancellationToken cancellationToken)
    {
        if (await sources.FindAsync(kind, key, cancellationToken) is not { } source)
        {
            return Result.Failure<IReadOnlyList<ContentTranslationDto>>(ErrorCodes.NotFound);
        }

        var list = await translations.ListForEntityAsync(kind, source.Id, cancellationToken);
        return Result.Success<IReadOnlyList<ContentTranslationDto>>(list.Select(t => ToDto(t, source.Text.Hash)).ToArray());
    }

    public async Task<Result<ContentTranslationDto>> UpsertAsync(
        TranslatableContent kind,
        string key,
        string lang,
        ContentTranslationRequest request,
        Guid staffId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ContentLanguages.Targets.Contains(lang, StringComparer.Ordinal) || await sources.FindAsync(kind, key, cancellationToken) is not { } source)
        {
            return Result.Failure<ContentTranslationDto>(ErrorCodes.NotFound);
        }

        var shape = ContentShape.For(kind);
        var text = Clean(request, shape, source.Text);
        var errors = TranslationTextRules.Check(text, shape, source.Text);
        if (errors.Count > 0)
        {
            return Result.Failure<ContentTranslationDto>(ErrorCodes.ValidationFailed, errors);
        }

        var now = clock.UtcNow;
        var hash = source.Text.Hash;
        var translation = await translations.FindAsync(kind, source.Id, lang, cancellationToken);
        if (translation is null)
        {
            translation = ContentTranslation.Create(kind, source.Id, lang, hash, now);
            translations.Add(translation);
        }

        Apply(translation, text, hash, request.Approve, staffId, now);
        if (kind == TranslatableContent.News && await news.FindAsync(source.Id, includeDeleted: true, cancellationToken) is { } article)
        {
            var others = (await translations.ListForEntityAsync(kind, source.Id, cancellationToken)).Where(t => !string.Equals(t.Lang, lang, StringComparison.Ordinal));
            article.TitleSearch = NewsSearchText.Build(article, [.. others, translation]);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Invalidate();
        return Result.Success(ToDto(translation, hash));
    }

    private SourceText Clean(ContentTranslationRequest request, ContentShape shape, SourceText source)
    {
        var body = shape.HasBody && source.BodyHtml is not null && !string.IsNullOrWhiteSpace(request.BodyHtml)
            ? (shape.RichBody ? sanitizer.SanitizeRich(request.BodyHtml) : sanitizer.Sanitize(request.BodyHtml))
            : null;
        return new SourceText(
            Trim(request.Title),
            shape.SummaryMax is not null && source.Summary is not null ? Trim(request.Summary) : null,
            body,
            shape.CtaMax is not null && source.CtaText is not null ? Trim(request.CtaText) : null,
            shape.HasMeta && source.MetaTitle is not null ? Trim(request.MetaTitle) : null,
            shape.HasMeta && source.MetaDescription is not null ? Trim(request.MetaDescription) : null);
    }

    private static void Apply(ContentTranslation translation, SourceText text, string hash, bool approve, Guid staffId, DateTime now)
    {
        translation.Title = text.Title;
        translation.Summary = text.Summary;
        translation.BodyHtml = text.BodyHtml;
        translation.CtaText = text.CtaText;
        translation.MetaTitle = text.MetaTitle;
        translation.MetaDescription = text.MetaDescription;
        translation.SourceHash = hash;
        translation.TranslatedByStaffId = staffId;
        translation.ReviewStatus = approve ? TranslationStatus.Approved : TranslationStatus.Draft;
        translation.ReviewedByStaffId = approve ? staffId : null;
        translation.ReviewedAtUtc = approve ? now : null;
        translation.UpdatedAtUtc = now;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ContentTranslationDto ToDto(ContentTranslation t, string currentHash) =>
        new(t.Lang, t.Title, t.Summary, t.BodyHtml, t.CtaText, t.MetaTitle, t.MetaDescription, t.ReviewStatus,
            !string.Equals(t.SourceHash, currentHash, StringComparison.Ordinal), t.UpdatedAtUtc);
}
