using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Menu.Services;

namespace ObentoTensho.Core.Content.Services;

// FR-CNT-03: danh mục tin; xóa khi còn bài ⇒ 409 (BR-CNT-42)
public sealed class NewsCategoryService(
    INewsRepository news,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IValidator<NewsCategoryRequest> validator)
{
    private const int SlugMaxLength = 150;
    private const string Entity = "news_category";

    public async Task<IReadOnlyList<NewsCategoryDto>> ListAsync(CancellationToken cancellationToken) =>
        (await news.ListCategoriesAsync(activeOnly: false, cancellationToken)).Select(ToDto).ToArray();

    public async Task<Result<NewsCategoryDto>> CreateAsync(NewsCategoryRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<NewsCategoryDto>();
        }

        if (request.Slug is { } requested && await news.CategorySlugExistsAsync(requested, null, cancellationToken))
        {
            return Result.Failure<NewsCategoryDto>(ErrorCodes.DuplicateName);
        }

        var category = new NewsCategory
        {
            Name = request.Name.Trim(),
            Slug = await SlugGenerator.UniqueAsync(request.Slug, request.Name, SlugMaxLength, s => news.CategorySlugExistsAsync(s, null, cancellationToken)),
            IsActive = request.IsActive,
        };
        return await unitOfWork.TryCreateAsync(async ct =>
        {
            news.AddCategory(category);
            await unitOfWork.SaveChangesAsync(ct);
            audit.Record(new AuditEntry($"content.{Entity}.created", Entity, Id(category), After: new { category.Name, category.Slug }));
            await unitOfWork.SaveChangesAsync(ct);
            return ToDto(category);
        }, ErrorCodes.DuplicateName, cancellationToken);
    }

    public async Task<Result<NewsCategoryDto>> UpdateAsync(int id, NewsCategoryRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<NewsCategoryDto>();
        }

        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure<NewsCategoryDto>(ErrorCodes.PreconditionRequired);
        }

        if (await news.FindCategoryAsync(id, cancellationToken) is not { } category)
        {
            return Result.Failure<NewsCategoryDto>(ErrorCodes.NotFound);
        }

        if (request.Slug is { } slug && await news.CategorySlugExistsAsync(slug, id, cancellationToken))
        {
            return Result.Failure<NewsCategoryDto>(ErrorCodes.DuplicateName);
        }

        unitOfWork.ExpectRowVersion(category, version);
        category.Name = request.Name.Trim();
        category.Slug = request.Slug ?? category.Slug;
        category.IsActive = request.IsActive;
        audit.Record(new AuditEntry($"content.{Entity}.updated", Entity, Id(category), After: new { category.Name, category.Slug, category.IsActive }));
        return await unitOfWork.TrySaveAsync(() => ToDto(category), ErrorCodes.DuplicateName, cancellationToken);
    }

    public async Task<Result> DeleteAsync(int id, string? ifMatch, CancellationToken cancellationToken)
    {
        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure(ErrorCodes.PreconditionRequired);
        }

        if (await news.FindCategoryAsync(id, cancellationToken) is not { } category)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        if (await news.CategoryHasNewsAsync(id, cancellationToken))
        {
            return Result.Failure(ErrorCodes.CategoryHasItems);
        }

        unitOfWork.ExpectRowVersion(category, version);
        audit.Record(new AuditEntry($"content.{Entity}.deleted", Entity, Id(category), Before: new { category.Name, category.Slug }));
        news.RemoveCategory(category);
        return await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, cancellationToken);
    }

    private static NewsCategoryDto ToDto(NewsCategory c) =>
        new(c.Id, c.Name, c.Slug, c.DisplayOrder, c.IsActive, RowVersions.Encode(c.RowVersion));

    private static string Id(NewsCategory category) => category.Id.ToString(CultureInfo.InvariantCulture);
}
