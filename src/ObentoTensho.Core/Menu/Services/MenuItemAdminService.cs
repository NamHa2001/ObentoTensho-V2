using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Menu.Services;

// Món (docs/srs/15 §3.2): món + nhóm tùy chọn lưu trong một transaction (BR-MNU-12), audit đổi giá/trạng thái (BR-MNU-13)
public sealed class MenuItemAdminService(
    IMenuRepository menu,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    IMenuCache cache,
    IHtmlSanitizer sanitizer,
    IClock clock,
    IValidator<MenuItemRequest> validator)
{
    public const int MaxPageSize = 100;
    private const string Entity = "menu_item";

    public async Task<PagedResponse<AdminMenuItemDto>> ListAsync(MenuItemQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var (items, total) = await menu.ListItemsAsync(query with { Page = page, PageSize = pageSize }, cancellationToken);
        var dtos = new List<AdminMenuItemDto>(items.Count);
        foreach (var item in items)
        {
            dtos.Add(item.ToAdmin(await menu.GetItemGroupIdsAsync(item.Id, cancellationToken)));
        }

        return new PagedResponse<AdminMenuItemDto>(dtos, new PageMeta(page, pageSize, total));
    }

    public async Task<Result<AdminMenuItemDto>> GetAsync(int id, CancellationToken cancellationToken) =>
        await menu.FindItemAsync(id, cancellationToken) is { } item
            ? Result.Success(item.ToAdmin(await menu.GetItemGroupIdsAsync(id, cancellationToken)))
            : Result.Failure<AdminMenuItemDto>(ErrorCodes.NotFound);

    public async Task<Result<AdminMenuItemDto>> CreateAsync(MenuItemRequest request, CancellationToken cancellationToken)
    {
        if (await CheckAsync(request, cancellationToken) is { } failure)
        {
            return Result.Failure<AdminMenuItemDto>(failure.ErrorCode ?? ErrorCodes.ValidationFailed, failure.FieldErrors);
        }

        var slug = await SlugGenerator.UniqueAsync(request.Slug, request.NameVi, 200, s => menu.ItemSlugExistsAsync(s, null, cancellationToken));
        var result = await unitOfWork.TryCreateAsync(async ct =>
        {
            var item = new MenuItem { CategoryId = request.CategoryId, NameVi = string.Empty, Slug = slug, NameSearch = string.Empty, Price = request.Price, CreatedAtUtc = clock.UtcNow };
            MenuItemMapping.Apply(item, request, sanitizer, clock.UtcNow);
            item.NameSearch = MenuItemMapping.SearchText(item, []);
            menu.AddItem(item);
            await unitOfWork.SaveChangesAsync(ct);
            await menu.ReplaceItemGroupsAsync(item.Id, request.OptionGroupIds ?? [], ct);
            audit.Record(new AuditEntry($"menu.{Entity}.created", Entity, Id(item.Id), After: MenuItemMapping.Snapshot(item)));
            await unitOfWork.SaveChangesAsync(ct);
            return item.ToAdmin(request.OptionGroupIds ?? []);
        }, ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    public async Task<Result<AdminMenuItemDto>> UpdateAsync(int id, MenuItemRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        if (await CheckAsync(request, cancellationToken) is { } failure)
        {
            return Result.Failure<AdminMenuItemDto>(failure.ErrorCode ?? ErrorCodes.ValidationFailed, failure.FieldErrors);
        }

        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure<AdminMenuItemDto>(ErrorCodes.PreconditionRequired);
        }

        if (request.Slug is { } slug && await menu.ItemSlugExistsAsync(slug, id, cancellationToken))
        {
            return Result.Failure<AdminMenuItemDto>(ErrorCodes.DuplicateName);
        }

        var result = await unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (await menu.FindItemAsync(id, ct) is not { } item)
            {
                return Result.Failure<AdminMenuItemDto>(ErrorCodes.NotFound);
            }

            var before = MenuItemMapping.Snapshot(item);
            unitOfWork.ExpectRowVersion(item, version);
            item.Slug = request.Slug ?? item.Slug;
            MenuItemMapping.Apply(item, request, sanitizer, clock.UtcNow);
            item.NameSearch = MenuItemMapping.SearchText(item, await menu.ListTranslationsAsync<MenuItemTranslation>(id, ct));
            await menu.ReplaceItemGroupsAsync(id, request.OptionGroupIds ?? [], ct);
            audit.Record(new AuditEntry($"menu.{Entity}.updated", Entity, Id(id), before, MenuItemMapping.Snapshot(item)));
            return await unitOfWork.TrySaveAsync(() => item.ToAdmin(request.OptionGroupIds ?? []), ErrorCodes.DuplicateName, ct);
        }, cancellationToken);
        cache.Invalidate();
        return result;
    }

    // BR-MNU-16: hết hàng mặc định tới hết hôm nay (giờ VN), sau đó tự bán lại
    public async Task<Result<AdminMenuItemDto>> SetStatusAsync(int id, MenuItemStatusRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status))
        {
            return Result.Failure<AdminMenuItemDto>(ErrorCodes.ValidationFailed);
        }

        if (await menu.FindItemAsync(id, cancellationToken) is not { } item)
        {
            return Result.Failure<AdminMenuItemDto>(ErrorCodes.NotFound);
        }

        var before = new { item.Status, item.SoldOutUntilUtc };
        item.Status = request.Status;
        item.SoldOutUntilUtc = request.Status == SaleStatus.SoldOut
            ? request.SoldOutUntilUtc ?? clock.ToUtc(clock.TodayLocal.AddDays(1), TimeOnly.MinValue)
            : null;
        item.UpdatedAtUtc = clock.UtcNow;
        audit.Record(new AuditEntry($"menu.{Entity}.status_changed", Entity, Id(id), before, new { item.Status, item.SoldOutUntilUtc }));
        var groups = await menu.GetItemGroupIdsAsync(id, cancellationToken);
        var result = await unitOfWork.TrySaveAsync(() => item.ToAdmin(groups), ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    // BR-MNU-01: xóa = IsDeleted, không dùng trạng thái Ẩn; đơn cũ giữ nguyên nhờ snapshot giá (BR-MNU-07)
    public async Task<Result> DeleteAsync(int id, string? ifMatch, CancellationToken cancellationToken)
    {
        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure(ErrorCodes.PreconditionRequired);
        }

        if (await menu.FindItemAsync(id, cancellationToken) is not { } item)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        unitOfWork.ExpectRowVersion(item, version);
        item.IsDeleted = true;
        item.DeletedAtUtc = clock.UtcNow;
        audit.Record(new AuditEntry($"menu.{Entity}.deleted", Entity, Id(id), Before: MenuItemMapping.Snapshot(item)));
        var result = await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    public async Task<Result> ReorderAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        var items = await menu.FindItemsAsync(ids, cancellationToken);
        if (ids.Distinct().Count() != ids.Count || items.Count != ids.Count || items.Select(i => i.CategoryId).Distinct().Count() > 1)
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

    private async Task<Result?> CheckAsync(MenuItemRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        if (await menu.FindCategoryAsync(request.CategoryId, cancellationToken) is null)
        {
            return Result.Failure(ErrorCodes.CategoryInactive);
        }

        var groupIds = request.OptionGroupIds ?? [];
        return groupIds.Count > 0 && await menu.CountExistingGroupsAsync(groupIds, cancellationToken) != groupIds.Count
            ? Result.Failure(ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal) { ["optionGroupIds"] = [FieldErrorCodes.InvalidFormat] })
            : null;
    }

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);
}
