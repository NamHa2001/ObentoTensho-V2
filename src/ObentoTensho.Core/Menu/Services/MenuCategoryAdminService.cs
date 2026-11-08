using System.Globalization;
using FluentValidation;
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

// Danh mục thực đơn (docs/srs/15 §3.1): đổi tên không đổi slug, xóa khi còn món ⇒ 409, sắp thứ tự một lần lưu
public sealed class MenuCategoryAdminService(
    IMenuRepository menu,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    IMenuCache cache,
    IImageRepository images,
    IClock clock,
    IValidator<CategoryRequest> validator)
{
    private const string Entity = "menu_category";

    public async Task<IReadOnlyList<AdminCategoryDto>> ListAsync(CancellationToken cancellationToken)
    {
        var counts = await menu.CountItemsByCategoryAsync(cancellationToken);
        return (await menu.ListCategoriesAsync(cancellationToken))
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id)
            .Select(c => c.ToAdmin(counts.GetValueOrDefault(c.Id)))
            .ToArray();
    }

    public async Task<Result<AdminCategoryDto>> CreateAsync(CategoryRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<AdminCategoryDto>();
        }

        if (await ImageReferences.CheckAsync(images, request.ImageId, "imageId", cancellationToken) is { } imageFailure)
        {
            return Result.Failure<AdminCategoryDto>(imageFailure.ErrorCode ?? ErrorCodes.ValidationFailed, imageFailure.FieldErrors);
        }

        var slug = await SlugGenerator.UniqueAsync(request.Slug, request.Name, 150, s => menu.CategorySlugExistsAsync(s, null, cancellationToken));
        var order = (await menu.ListCategoriesAsync(cancellationToken)).Select(c => c.DisplayOrder).DefaultIfEmpty(0).Max() + 1;
        var result = await unitOfWork.TryCreateAsync(async ct =>
        {
            var category = new MenuCategory { Name = request.Name.Trim(), Slug = slug, IsActive = request.IsActive, ImageId = request.ImageId, DisplayOrder = order, CreatedAtUtc = clock.UtcNow };
            menu.AddCategory(category);
            await unitOfWork.SaveChangesAsync(ct);
            audit.Record(new AuditEntry($"menu.{Entity}.created", Entity, Id(category.Id), After: new { category.Name, category.Slug, category.IsActive }));
            await unitOfWork.SaveChangesAsync(ct);
            return category.ToAdmin(0);
        }, ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    public async Task<Result<AdminCategoryDto>> UpdateAsync(int id, CategoryRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<AdminCategoryDto>();
        }

        if (await ImageReferences.CheckAsync(images, request.ImageId, "imageId", cancellationToken) is { } imageFailure)
        {
            return Result.Failure<AdminCategoryDto>(imageFailure.ErrorCode ?? ErrorCodes.ValidationFailed, imageFailure.FieldErrors);
        }

        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure<AdminCategoryDto>(ErrorCodes.PreconditionRequired);
        }

        if (await menu.FindCategoryAsync(id, cancellationToken) is not { } category)
        {
            return Result.Failure<AdminCategoryDto>(ErrorCodes.NotFound);
        }

        if (request.Slug is { } slug && !string.Equals(slug, category.Slug, StringComparison.Ordinal) && await menu.CategorySlugExistsAsync(slug, id, cancellationToken))
        {
            return Result.Failure<AdminCategoryDto>(ErrorCodes.DuplicateName);
        }

        var before = new { category.Name, category.Slug, category.IsActive, category.ImageId };
        unitOfWork.ExpectRowVersion(category, version);
        category.Name = request.Name.Trim();
        category.Slug = request.Slug ?? category.Slug;
        category.IsActive = request.IsActive;
        category.ImageId = request.ImageId;
        audit.Record(new AuditEntry($"menu.{Entity}.updated", Entity, Id(id), before, new { category.Name, category.Slug, category.IsActive, category.ImageId }));
        var counts = await menu.CountItemsByCategoryAsync(cancellationToken);
        var result = await unitOfWork.TrySaveAsync(() => category.ToAdmin(counts.GetValueOrDefault(id)), ErrorCodes.DuplicateName, cancellationToken);
        cache.Invalidate();
        return result;
    }

    // BR-MNU-08: còn món (kể cả đã xóa mềm) thì không xóa — ẩn danh mục thay vì xóa
    public async Task<Result> DeleteAsync(int id, string? ifMatch, CancellationToken cancellationToken)
    {
        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure(ErrorCodes.PreconditionRequired);
        }

        if (await menu.FindCategoryAsync(id, cancellationToken) is not { } category)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        if (await menu.CategoryHasItemsAsync(id, cancellationToken))
        {
            return Result.Failure(ErrorCodes.CategoryHasItems);
        }

        unitOfWork.ExpectRowVersion(category, version);
        menu.RemoveCategory(category);
        audit.Record(new AuditEntry($"menu.{Entity}.deleted", Entity, Id(id), Before: new { category.Name, category.Slug }));
        var result = await unitOfWork.TrySaveAsync(ErrorCodes.CategoryHasItems, cancellationToken);
        cache.Invalidate();
        return result;
    }

    // Danh sách gửi lên phải đúng bằng tập danh mục hiện có
    public async Task<Result> ReorderAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        var categories = await menu.FindCategoriesAsync(ids, cancellationToken);
        var all = await menu.ListCategoriesAsync(cancellationToken);
        if (ids.Distinct().Count() != ids.Count || categories.Count != ids.Count || all.Count != ids.Count)
        {
            return Result.Failure(ErrorCodes.ValidationFailed);
        }

        var position = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index + 1);
        foreach (var category in categories)
        {
            category.DisplayOrder = position[category.Id];
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Invalidate();
        return Result.Success();
    }

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);
}
