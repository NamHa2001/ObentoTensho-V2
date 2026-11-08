using System.Globalization;
using ObentoTensho.Contracts.Dtos.Media;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Services;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Menu.Services;

// BR-MNU-06/12: ảnh chính + tối đa 4 ảnh phụ, chỉ mã ảnh đã qua pipeline (BR-CNT-30), thay cả bộ trong một transaction.
// Tách khỏi form món để nhập Excel không đụng tới ảnh
public sealed class MenuItemImageService(
    IMenuRepository menu,
    IImageRepository images,
    ImageMapper imageMapper,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    IMenuCache cache,
    IClock clock)
{
    private const string Entity = "menu_item";

    public async Task<Result<MenuItemImagesDto>> GetAsync(int id, CancellationToken cancellationToken)
    {
        if (await menu.FindItemAsync(id, cancellationToken) is not { } item)
        {
            return Result.Failure<MenuItemImagesDto>(ErrorCodes.NotFound);
        }

        var gallery = (await menu.ListItemImagesAsync(id, cancellationToken)).Select(i => i.ImageId).ToArray();
        return Result.Success(await ToDtoAsync(item.MainImageId, gallery, cancellationToken));
    }

    public async Task<Result<MenuItemImagesDto>> ReplaceAsync(int id, MenuItemImagesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var gallery = request.ImageIds ?? [];
        if (gallery.Count > MenuItemImage.MaxPerItem || gallery.Distinct().Count() != gallery.Count)
        {
            return Invalid("imageIds", gallery.Count > MenuItemImage.MaxPerItem ? FieldErrorCodes.TooLong : FieldErrorCodes.InvalidFormat);
        }

        var wanted = request.MainImageId is { } main ? [main, .. gallery] : gallery.ToArray();
        var loaded = await images.LoadAsync(wanted, cancellationToken);
        if (!wanted.All(loaded.ContainsKey))
        {
            return Invalid(request.MainImageId is { } m && !loaded.ContainsKey(m) ? "mainImageId" : "imageIds", FieldErrorCodes.NotFound);
        }

        var result = await unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (await menu.FindItemAsync(id, ct) is not { } item)
            {
                return Result.Failure<bool>(ErrorCodes.NotFound);
            }

            var before = new { item.MainImageId, ImageIds = (await menu.ListItemImagesAsync(id, ct)).Select(i => i.ImageId).ToArray() };
            item.MainImageId = request.MainImageId;
            item.UpdatedAtUtc = clock.UtcNow;
            await menu.ReplaceItemImagesAsync(id, gallery, ct);
            audit.Record(new AuditEntry($"menu.{Entity}.images_updated", Entity, id.ToString(CultureInfo.InvariantCulture), before, new { request.MainImageId, ImageIds = gallery }));
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(true);
        }, cancellationToken);
        if (!result.IsSuccess)
        {
            return Result.Failure<MenuItemImagesDto>(result.ErrorCode ?? ErrorCodes.NotFound);
        }

        cache.Invalidate();
        return Result.Success(await ToDtoAsync(request.MainImageId, gallery, cancellationToken));
    }

    private async Task<MenuItemImagesDto> ToDtoAsync(Guid? mainImageId, IReadOnlyList<Guid> gallery, CancellationToken cancellationToken)
    {
        var ids = mainImageId is { } main ? [main, .. gallery] : gallery.ToArray();
        var loaded = await images.LoadAsync(ids, cancellationToken);
        ImageDto? Map(Guid imageId) => loaded.TryGetValue(imageId, out var image) ? imageMapper.ToDto(image) : null;
        return new MenuItemImagesDto(mainImageId is { } id ? Map(id) : null, gallery.Select(Map).OfType<ImageDto>().ToArray());
    }

    private static Result<MenuItemImagesDto> Invalid(string field, string code) =>
        Result.Failure<MenuItemImagesDto>(ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [code] });
}
