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
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Domain;
using ObentoTensho.Core.Media.Services;

namespace ObentoTensho.Core.Content.Services;

// FR-CNT-02: thư viện ảnh/video
public sealed class GalleryAdminService(
    IContentRepository content,
    IImageRepository images,
    ImageMapper imageMapper,
    IPublicContentCache cache,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IValidator<GalleryItemRequest> validator)
{
    private const string Entity = "gallery_item";

    public async Task<IReadOnlyList<AdminGalleryItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var items = await content.ListGalleryAsync(activeOnly: false, category: null, cancellationToken);
        var loaded = await images.LoadAsync(items.Select(i => i.ImageId).OfType<Guid>().Distinct().ToArray(), cancellationToken);
        return items.Select(i => ToDto(i, loaded)).ToArray();
    }

    public async Task<Result<AdminGalleryItemDto>> CreateAsync(GalleryItemRequest request, CancellationToken cancellationToken)
    {
        if (await CheckAsync(request, cancellationToken) is { } failure)
        {
            return Result.Failure<AdminGalleryItemDto>(failure.ErrorCode ?? ErrorCodes.ValidationFailed, failure.FieldErrors);
        }

        var item = new GalleryItem { DisplayOrder = (await content.FindGalleryItemsAsync(cancellationToken)).Count + 1 };
        Apply(item, request);
        var result = await unitOfWork.TryCreateAsync(async ct =>
        {
            content.AddGalleryItem(item);
            await unitOfWork.SaveChangesAsync(ct);
            audit.Record(new AuditEntry($"content.{Entity}.created", Entity, Id(item), After: new { item.MediaType, item.Category }));
            await unitOfWork.SaveChangesAsync(ct);
            return await ToDtoAsync(item, ct);
        }, ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    public async Task<Result<AdminGalleryItemDto>> UpdateAsync(int id, GalleryItemRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        if (await CheckAsync(request, cancellationToken) is { } failure)
        {
            return Result.Failure<AdminGalleryItemDto>(failure.ErrorCode ?? ErrorCodes.ValidationFailed, failure.FieldErrors);
        }

        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure<AdminGalleryItemDto>(ErrorCodes.PreconditionRequired);
        }

        if (await content.FindGalleryItemAsync(id, cancellationToken) is not { } item)
        {
            return Result.Failure<AdminGalleryItemDto>(ErrorCodes.NotFound);
        }

        unitOfWork.ExpectRowVersion(item, version);
        Apply(item, request);
        audit.Record(new AuditEntry($"content.{Entity}.updated", Entity, Id(item), After: new { item.MediaType, item.Category, item.IsActive }));
        var saved = await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return saved.IsSuccess
            ? Result.Success(await ToDtoAsync(item, cancellationToken))
            : Result.Failure<AdminGalleryItemDto>(saved.ErrorCode ?? ErrorCodes.PreconditionFailed);
    }

    public async Task<Result> DeleteAsync(int id, string? ifMatch, CancellationToken cancellationToken)
    {
        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure(ErrorCodes.PreconditionRequired);
        }

        if (await content.FindGalleryItemAsync(id, cancellationToken) is not { } item)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        unitOfWork.ExpectRowVersion(item, version);
        audit.Record(new AuditEntry($"content.{Entity}.deleted", Entity, Id(item), Before: new { item.MediaType, item.Category }));
        content.RemoveGalleryItem(item);
        var result = await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    // BR-CNT-31
    public async Task<Result> ReorderAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        var items = await content.FindGalleryItemsAsync(cancellationToken);
        if (!ids.Order().SequenceEqual(items.Select(i => i.Id).Order()))
        {
            return Result.Failure(ErrorCodes.ValidationFailed);
        }

        var position = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index + 1);
        foreach (var item in items)
        {
            item.DisplayOrder = position[item.Id];
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Invalidate();
        return Result.Success();
    }

    private async Task<Result?> CheckAsync(GalleryItemRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        return request.ImageId is { } id && !(await images.LoadAsync([id], cancellationToken)).ContainsKey(id)
            ? Result.Failure(ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal) { ["imageId"] = [FieldErrorCodes.NotFound] })
            : null;
    }

    private static void Apply(GalleryItem item, GalleryItemRequest request)
    {
        item.MediaType = request.MediaType;
        item.ImageId = request.MediaType == GalleryMediaType.Image ? request.ImageId : null;
        item.VideoUrl = request.MediaType == GalleryMediaType.Video ? request.VideoUrl : null;
        item.Category = request.Category;
        item.Caption = string.IsNullOrWhiteSpace(request.Caption) ? null : request.Caption.Trim();
        item.IsActive = request.IsActive;
    }

    private async Task<AdminGalleryItemDto> ToDtoAsync(GalleryItem item, CancellationToken cancellationToken) =>
        ToDto(item, await images.LoadAsync(item.ImageId is { } id ? [id] : [], cancellationToken));

    private AdminGalleryItemDto ToDto(GalleryItem i, IReadOnlyDictionary<Guid, StoredImage> loaded) =>
        new(
            i.Id,
            i.MediaType,
            i.ImageId is { } id && loaded.TryGetValue(id, out var image) ? imageMapper.ToDto(image) : null,
            i.VideoUrl,
            i.Category,
            i.Caption,
            i.DisplayOrder,
            i.IsActive,
            RowVersions.Encode(i.RowVersion));

    private static string Id(GalleryItem item) => item.Id.ToString(CultureInfo.InvariantCulture);
}
