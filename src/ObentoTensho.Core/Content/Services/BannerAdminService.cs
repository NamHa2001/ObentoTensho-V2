using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Services;

namespace ObentoTensho.Core.Content.Services;

// FR-CNT-01: banner trang chủ
public sealed class BannerAdminService(
    IContentRepository content,
    IImageRepository images,
    ImageMapper imageMapper,
    IPublicContentCache cache,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IValidator<BannerRequest> validator)
{
    private const string Entity = "banner";

    public async Task<IReadOnlyList<AdminBannerDto>> ListAsync(CancellationToken cancellationToken)
    {
        var banners = await content.ListBannersAsync(activeOnly: false, cancellationToken);
        var loaded = await images.LoadAsync(banners.SelectMany(ImageIds).Distinct().ToArray(), cancellationToken);
        return banners.Select(b => ToDto(b, loaded)).ToArray();
    }

    public async Task<Result<AdminBannerDto>> CreateAsync(BannerRequest request, CancellationToken cancellationToken)
    {
        if (await CheckAsync(request, cancellationToken) is { } failure)
        {
            return Result.Failure<AdminBannerDto>(failure.ErrorCode ?? ErrorCodes.ValidationFailed, failure.FieldErrors);
        }

        var banner = new Banner { ImageId = request.ImageId, Title = request.Title.Trim(), DisplayOrder = (await content.FindBannersAsync(cancellationToken)).Count + 1 };
        Apply(banner, request);
        var result = await unitOfWork.TryCreateAsync(async ct =>
        {
            content.AddBanner(banner);
            await unitOfWork.SaveChangesAsync(ct);
            audit.Record(new AuditEntry($"content.{Entity}.created", Entity, Id(banner), After: new { banner.Title, banner.CtaLink }));
            await unitOfWork.SaveChangesAsync(ct);
            return await ToDtoAsync(banner, ct);
        }, ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    public async Task<Result<AdminBannerDto>> UpdateAsync(int id, BannerRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        if (await CheckAsync(request, cancellationToken) is { } failure)
        {
            return Result.Failure<AdminBannerDto>(failure.ErrorCode ?? ErrorCodes.ValidationFailed, failure.FieldErrors);
        }

        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure<AdminBannerDto>(ErrorCodes.PreconditionRequired);
        }

        if (await content.FindBannerAsync(id, cancellationToken) is not { } banner)
        {
            return Result.Failure<AdminBannerDto>(ErrorCodes.NotFound);
        }

        unitOfWork.ExpectRowVersion(banner, version);
        var before = new { banner.Title, banner.CtaLink, banner.IsActive };
        Apply(banner, request);
        audit.Record(new AuditEntry($"content.{Entity}.updated", Entity, Id(banner), before, new { banner.Title, banner.CtaLink, banner.IsActive }));
        var saved = await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return saved.IsSuccess
            ? Result.Success(await ToDtoAsync(banner, cancellationToken))
            : Result.Failure<AdminBannerDto>(saved.ErrorCode ?? ErrorCodes.PreconditionFailed);
    }

    // BR-CNT-32: ảnh của banner đã xóa ở lại, thành ảnh mồ côi
    public async Task<Result> DeleteAsync(int id, string? ifMatch, CancellationToken cancellationToken)
    {
        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure(ErrorCodes.PreconditionRequired);
        }

        if (await content.FindBannerAsync(id, cancellationToken) is not { } banner)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        unitOfWork.ExpectRowVersion(banner, version);
        audit.Record(new AuditEntry($"content.{Entity}.deleted", Entity, Id(banner), Before: new { banner.Title }));
        content.RemoveBanner(banner);
        var result = await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    // BR-CNT-31: nhận đủ danh sách id, từ chối id lạ, thiếu hoặc trùng; lưu một lần
    public async Task<Result> ReorderAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        var banners = await content.FindBannersAsync(cancellationToken);
        if (!ids.Order().SequenceEqual(banners.Select(b => b.Id).Order()))
        {
            return Result.Failure(ErrorCodes.ValidationFailed);
        }

        var position = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index + 1);
        foreach (var banner in banners)
        {
            banner.DisplayOrder = position[banner.Id];
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Invalidate();
        return Result.Success();
    }

    private async Task<Result?> CheckAsync(BannerRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        var ids = request.ImageMobileId is { } mobile ? new[] { request.ImageId, mobile } : [request.ImageId];
        var loaded = await images.LoadAsync(ids, cancellationToken);
        return ids.All(loaded.ContainsKey)
            ? null
            : Result.Failure(ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal) { ["imageId"] = [FieldErrorCodes.NotFound] });
    }

    private static void Apply(Banner banner, BannerRequest request)
    {
        banner.ImageId = request.ImageId;
        banner.ImageMobileId = request.ImageMobileId;
        banner.Title = request.Title.Trim();
        banner.Subtitle = Clean(request.Subtitle);
        banner.CtaText = Clean(request.CtaText);
        banner.CtaLink = Clean(request.CtaLink);
        banner.ShowFromDate = request.ShowFromDate;
        banner.ShowToDate = request.ShowToDate;
        banner.IsActive = request.IsActive;
    }

    private async Task<AdminBannerDto> ToDtoAsync(Banner banner, CancellationToken cancellationToken) =>
        ToDto(banner, await images.LoadAsync(ImageIds(banner).ToArray(), cancellationToken));

    private AdminBannerDto ToDto(Banner b, IReadOnlyDictionary<Guid, Media.Domain.StoredImage> loaded) =>
        new(
            b.Id,
            loaded.TryGetValue(b.ImageId, out var image) ? imageMapper.ToDto(image) : null,
            b.ImageMobileId is { } m && loaded.TryGetValue(m, out var mobile) ? imageMapper.ToDto(mobile) : null,
            b.Title,
            b.Subtitle,
            b.CtaText,
            b.CtaLink,
            b.DisplayOrder,
            b.ShowFromDate,
            b.ShowToDate,
            b.IsActive,
            RowVersions.Encode(b.RowVersion));

    private static IEnumerable<Guid> ImageIds(Banner banner) =>
        banner.ImageMobileId is { } mobile ? [banner.ImageId, mobile] : [banner.ImageId];

    private static string Id(Banner banner) => banner.Id.ToString(CultureInfo.InvariantCulture);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
