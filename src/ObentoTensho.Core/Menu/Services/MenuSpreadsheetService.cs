using System.Globalization;
using FluentValidation;
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

// BR-MNU-10, BR-MNU-14: xuất/nhập món và danh mục bằng Excel. Nhập: mỗi dòng qua cùng validator với form tạo món/danh mục,
// báo lỗi theo dòng, mặc định chỉ xem trước; ghi thật chỉ khi commit=true và mọi dòng hợp lệ (tất cả hoặc không).
// Cập nhật bằng tệp ghi đè theo slug, không dùng ETag — xem trước rồi ghi nên người nhập thấy trước thay đổi.
public sealed class MenuSpreadsheetService(
    IMenuRepository menu,
    IMenuSpreadsheet spreadsheet,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    IMenuCache cache,
    IHtmlSanitizer sanitizer,
    IClock clock,
    IValidator<MenuItemRequest> validator,
    IValidator<CategoryRequest> categoryValidator)
{
    private const string Entity = "menu_item";
    private const string CategoryEntity = "menu_category";
    private const int SlugMaxLength = 200;
    private const int CategorySlugMaxLength = 150;

    // Mã tạm cho danh mục sẽ được tạo trong cùng tệp; lúc ghi đổi sang mã thật theo slug
    private const int PendingCategoryId = int.MaxValue;

    // Lỗi validator của form → cột trong mẫu
    private static readonly Dictionary<string, string> ColumnOf = new(StringComparer.Ordinal)
    {
        [nameof(MenuItemRequest.CategoryId)] = MenuSheet.Category,
        [nameof(MenuItemRequest.NameVi)] = MenuSheet.Name,
        [nameof(MenuItemRequest.Slug)] = MenuSheet.Slug,
        [nameof(MenuItemRequest.ShortDesc)] = MenuSheet.ShortDesc,
        [nameof(MenuItemRequest.Price)] = MenuSheet.Price,
        [nameof(MenuItemRequest.OriginalPrice)] = MenuSheet.OriginalPrice,
        [nameof(MenuItemRequest.Status)] = MenuSheet.Status,
        [nameof(MenuItemRequest.Allergens)] = MenuSheet.Allergens,
    };

    // Tên tệp theo ngày giờ VN, vd thuc-don-2026-11-07.xlsx
    public string FileName(string prefix) =>
        string.Create(CultureInfo.InvariantCulture, $"{prefix}-{clock.TodayLocal:yyyy-MM-dd}.xlsx");

    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken)
    {
        var categories = await menu.ListCategoriesAsync(cancellationToken);
        var slugOf = categories.ToDictionary(c => c.Id, c => c.Slug);
        var rows = (await menu.ListAllItemsAsync(cancellationToken))
            .Select(i => MenuSheetRows.ToRow(i, slugOf.GetValueOrDefault(i.CategoryId, string.Empty)))
            .ToArray();
        return spreadsheet.Write(new MenuWorkbook(rows, categories.Select(MenuSheetRows.ToRow).ToArray(), MenuSheet.Guide));
    }

    public async Task<byte[]> TemplateAsync(CancellationToken cancellationToken)
    {
        var categories = await menu.ListCategoriesAsync(cancellationToken);
        return spreadsheet.Write(new MenuWorkbook([], categories.Select(MenuSheetRows.ToRow).ToArray(), MenuSheet.Guide));
    }

    public async Task<Result<MenuImportReport>> ImportAsync(Stream content, bool commit, CancellationToken cancellationToken)
    {
        var read = spreadsheet.Read(content);
        if (!read.IsSuccess)
        {
            return Result.Failure<MenuImportReport>(read.ErrorCode ?? ErrorCodes.FileInvalid);
        }

        var plan = await PlanAsync(read.Value, cancellationToken);
        var report = plan.Report(committed: false);
        if (!commit || report.Failed > 0 || report.Categories.Any(c => c.Errors.Count > 0) || (report.Total == 0 && !plan.Categories.Any(c => c.Changed)))
        {
            return Result.Success(report);
        }

        Result<bool> applied;
        try
        {
            applied = await unitOfWork.TryInTransactionAsync(ct => ApplyAsync(plan, ct), cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // Có người tạo món cùng slug giữa lúc xem trước và lúc ghi: không ghi gì, nhập lại để xem báo cáo mới
            return Result.Failure<MenuImportReport>(ErrorCodes.DuplicateName);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure<MenuImportReport>(ErrorCodes.PreconditionFailed);
        }
        finally
        {
            cache.Invalidate();
        }

        return applied.IsSuccess ? Result.Success(plan.Report(committed: true)) : Result.Failure<MenuImportReport>(applied.ErrorCode ?? ErrorCodes.PreconditionFailed);
    }

    private async Task<ImportPlan> PlanAsync(MenuSheetData data, CancellationToken cancellationToken)
    {
        var existingCategories = (await menu.ListCategoriesAsync(cancellationToken)).ToDictionary(c => c.Slug, StringComparer.OrdinalIgnoreCase);
        var categories = existingCategories.ToDictionary(c => c.Key, c => c.Value.Id, StringComparer.OrdinalIgnoreCase);
        var plan = new ImportPlan();
        await PlanCategoriesAsync(data.Categories, existingCategories, plan, cancellationToken);
        foreach (var created in plan.Categories.Where(c => c.ExistingSlug is null && c.Slug is not null))
        {
            categories.TryAdd(created.Slug!, PendingCategoryId);
        }

        var rows = data.Items;
        var slugs = rows.Select(r => r[MenuSheet.Slug]).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var existing = (await menu.FindItemsBySlugAsync(slugs, cancellationToken)).ToDictionary(i => i.Slug, StringComparer.OrdinalIgnoreCase);
        var taken = await menu.TakenItemSlugsAsync(slugs, cancellationToken);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var errors = new List<MenuImportError>();
            var parsed = MenuSheetRows.Parse(row, errors);
            var item = parsed.Slug is { } slug ? existing.GetValueOrDefault(slug) : null;
            if (parsed.Slug is { } s && (!seen.Add(s) || (item is null && taken.Contains(s))))
            {
                errors.Add(new MenuImportError(MenuSheet.Slug, FieldErrorCodes.Duplicate));
            }

            var categoryId = parsed.CategorySlug is { } c ? categories.GetValueOrDefault(c) : 0;
            if (parsed.CategorySlug is not null && categoryId == 0)
            {
                errors.Add(new MenuImportError(MenuSheet.Category, FieldErrorCodes.NotFound));
            }

            // Mô tả đầy đủ và nhóm tùy chọn không có trong tệp: giữ nguyên của món đang có
            var request = new MenuItemRequest(
                categoryId, parsed.Name, parsed.Slug, parsed.ShortDesc, item?.FullDescHtml, parsed.Price, parsed.OriginalPrice,
                parsed.Labels, parsed.AllowTakeaway, parsed.Status, parsed.Allergens, []);
            await AddValidationErrorsAsync(request, errors, cancellationToken);
            plan.Add(new PlannedRow(row.RowNumber, request, parsed.CategorySlug, parsed.DisplayOrder, item?.Slug, errors));
        }

        return plan;
    }

    private async Task PlanCategoriesAsync(IReadOnlyList<MenuSheetRow> rows, Dictionary<string, MenuCategory> existing, ImportPlan plan, CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var errors = new List<MenuImportError>();
            var parsed = MenuSheetRows.ParseCategory(row, errors);
            var current = parsed.Slug is { } slug ? existing.GetValueOrDefault(slug) : null;
            var exists = current is not null;

            // Slug mới trùng slug cũ đã đổi (lịch sử chuyển hướng) cũng không dùng được
            if (parsed.Slug is { } s && (!seen.Add(s) || (!exists && await menu.CategorySlugExistsAsync(s, null, cancellationToken))))
            {
                errors.Add(new MenuImportError(MenuSheet.Slug, FieldErrorCodes.Duplicate));
            }

            var failedColumns = errors.Select(e => e.Column).ToHashSet(StringComparer.Ordinal);
            var validation = await categoryValidator.ValidateAsync(new CategoryRequest(parsed.Name, parsed.Slug, parsed.IsActive), cancellationToken);
            errors.AddRange(validation.Errors
                .Select(e => new MenuImportError(string.Equals(e.PropertyName, nameof(CategoryRequest.Name), StringComparison.Ordinal) ? MenuSheet.CategoryName : MenuSheet.Slug, e.ErrorCode))
                .Where(e => !failedColumns.Contains(e.Column))
                .DistinctBy(e => (e.Column, e.Code)));
            // Dòng giống hệt dữ liệu đang có (vd nhập lại tệp vừa xuất) không tính là thay đổi
            var changed = current is null
                || !string.Equals(current.Name, parsed.Name.Trim(), StringComparison.Ordinal)
                || current.IsActive != parsed.IsActive
                || (parsed.DisplayOrder is { } order && order != current.DisplayOrder);
            plan.AddCategory(new PlannedCategory(row.RowNumber, parsed, exists ? parsed.Slug : null, changed, errors));
        }
    }

    private async Task AddValidationErrorsAsync(MenuItemRequest request, List<MenuImportError> errors, CancellationToken cancellationToken)
    {
        // Cột đã lỗi kiểu dữ liệu thì không báo thêm lỗi quy tắc cho cùng cột
        var failedColumns = errors.Select(e => e.Column).ToHashSet(StringComparer.Ordinal);
        var validation = await validator.ValidateAsync(request, cancellationToken);
        errors.AddRange(validation.Errors
            .Select(e => new MenuImportError(ColumnOf.GetValueOrDefault(e.PropertyName.Split('.', '[')[0], e.PropertyName), e.ErrorCode))
            .Where(e => !failedColumns.Contains(e.Column))
            .DistinctBy(e => (e.Column, e.Code)));
    }

    // Chạy trong transaction có thử lại: transaction bắt đầu bằng context sạch nên phải nạp lại món ở đây,
    // không dùng bản đã nạp lúc lập kế hoạch (bản đó không còn được theo dõi, sửa vào sẽ không được lưu)
    private async Task<Result<bool>> ApplyAsync(ImportPlan plan, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (await ApplyCategoriesAsync(plan, now, cancellationToken) is not { } categoryIds)
        {
            // Danh mục bị xóa sau lúc xem trước
            return Result.Failure<bool>(ErrorCodes.PreconditionFailed);
        }

        int CategoryOf(PlannedRow row) => row.CategorySlug is { } slug ? categoryIds[slug] : row.Request.CategoryId;

        var updates = plan.Rows.Where(r => r.ExistingSlug is not null).ToArray();
        var items = (await menu.FindItemsBySlugAsync(updates.Select(r => r.ExistingSlug!).ToArray(), cancellationToken))
            .ToDictionary(i => i.Slug, StringComparer.OrdinalIgnoreCase);
        if (items.Count != updates.Length)
        {
            // Món bị xóa sau lúc xem trước
            return Result.Failure<bool>(ErrorCodes.PreconditionFailed);
        }

        var translations = await menu.ListItemTranslationsAsync(items.Values.Select(i => i.Id).ToArray(), cancellationToken);
        foreach (var row in updates)
        {
            var item = items[row.ExistingSlug!];
            var before = MenuItemMapping.Snapshot(item);
            MenuItemMapping.Apply(item, row.Request with { CategoryId = CategoryOf(row), FullDescHtml = item.FullDescHtml }, sanitizer, now);
            item.DisplayOrder = row.DisplayOrder ?? item.DisplayOrder;
            item.NameSearch = MenuItemMapping.SearchText(item, translations);
            audit.Record(new AuditEntry($"menu.{Entity}.updated", Entity, Id(item.Id), before, MenuItemMapping.Snapshot(item)));
        }

        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var created = new List<MenuItem>();
        foreach (var row in plan.Rows.Where(r => r.ExistingSlug is null))
        {
            var slug = await SlugGenerator.UniqueAsync(
                row.Request.Slug, row.Request.NameVi, SlugMaxLength, async s => reserved.Contains(s) || await menu.ItemSlugExistsAsync(s, null, cancellationToken));
            reserved.Add(slug);
            plan.CreatedSlugs[row.RowNumber] = slug;
            var request = row.Request with { CategoryId = CategoryOf(row) };
            var item = new MenuItem { CategoryId = request.CategoryId, NameVi = string.Empty, Slug = slug, NameSearch = string.Empty, Price = request.Price, CreatedAtUtc = now };
            MenuItemMapping.Apply(item, request, sanitizer, now);
            item.DisplayOrder = row.DisplayOrder ?? 0;
            item.NameSearch = MenuItemMapping.SearchText(item, []);
            menu.AddItem(item);
            created.Add(item);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var item in created)
        {
            audit.Record(new AuditEntry($"menu.{Entity}.created", Entity, Id(item.Id), After: MenuItemMapping.Snapshot(item)));
        }

        audit.Record(new AuditEntry($"menu.{Entity}.imported", Entity, "import", After: new { Created = created.Count, Updated = updates.Length, Categories = plan.Categories.Count }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }

    // Ghi danh mục trước món; trả slug → mã (cả danh mục cũ và mới), null nếu danh mục cần sửa đã bị xóa
    private async Task<Dictionary<string, int>?> ApplyCategoriesAsync(ImportPlan plan, DateTime now, CancellationToken cancellationToken)
    {
        var all = await menu.ListCategoriesAsync(cancellationToken);
        var idOf = all.ToDictionary(c => c.Slug, c => c.Id, StringComparer.OrdinalIgnoreCase);
        var updates = plan.Categories.Where(c => c.ExistingSlug is not null).ToArray();
        if (updates.Any(u => !idOf.ContainsKey(u.ExistingSlug!)))
        {
            return null;
        }

        var tracked = (await menu.FindCategoriesAsync(updates.Select(u => idOf[u.ExistingSlug!]).ToArray(), cancellationToken)).ToDictionary(c => c.Id);
        foreach (var row in updates)
        {
            var category = tracked[idOf[row.ExistingSlug!]];
            var before = new { category.Name, category.IsActive, category.DisplayOrder };
            category.Name = row.Parsed.Name.Trim();
            category.IsActive = row.Parsed.IsActive;
            category.DisplayOrder = row.Parsed.DisplayOrder ?? category.DisplayOrder;
            var after = new { category.Name, category.IsActive, category.DisplayOrder };
            if (!before.Equals(after))
            {
                audit.Record(new AuditEntry($"menu.{CategoryEntity}.updated", CategoryEntity, Id(category.Id), before, after));
            }
        }

        var nextOrder = all.Select(c => c.DisplayOrder).DefaultIfEmpty(0).Max() + 1;
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var created = new List<(PlannedCategory Row, MenuCategory Category)>();
        foreach (var row in plan.Categories.Where(c => c.ExistingSlug is null))
        {
            var slug = await SlugGenerator.UniqueAsync(
                row.Parsed.Slug, row.Parsed.Name, CategorySlugMaxLength, async s => reserved.Contains(s) || await menu.CategorySlugExistsAsync(s, null, cancellationToken));
            reserved.Add(slug);
            plan.CreatedCategorySlugs[row.RowNumber] = slug;
            var category = new MenuCategory { Name = row.Parsed.Name.Trim(), Slug = slug, IsActive = row.Parsed.IsActive, DisplayOrder = row.Parsed.DisplayOrder ?? nextOrder++, CreatedAtUtc = now };
            menu.AddCategory(category);
            created.Add((row, category));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var (row, category) in created)
        {
            idOf[category.Slug] = category.Id;
            audit.Record(new AuditEntry($"menu.{CategoryEntity}.created", CategoryEntity, Id(category.Id), After: new { category.Name, category.Slug, category.IsActive }));
        }

        return idOf;
    }

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);

    private sealed record PlannedRow(int RowNumber, MenuItemRequest Request, string? CategorySlug, int? DisplayOrder, string? ExistingSlug, IReadOnlyList<MenuImportError> Errors);

    private sealed record PlannedCategory(int RowNumber, MenuSheetRows.ParsedCategory Parsed, string? ExistingSlug, bool Changed, IReadOnlyList<MenuImportError> Errors)
    {
        public string? Slug => Parsed.Slug;
    }

    private sealed class ImportPlan
    {
        private readonly List<PlannedRow> _rows = [];
        private readonly List<PlannedCategory> _categories = [];

        public IReadOnlyList<PlannedRow> Rows => _rows;

        public List<PlannedCategory> Categories => _categories;

        public Dictionary<int, string> CreatedCategorySlugs { get; } = [];

        // Slug tự sinh của món mới, theo số dòng — để báo cáo sau khi ghi chỉ ra món vừa tạo
        public Dictionary<int, string> CreatedSlugs { get; } = [];

        public void Add(PlannedRow row) => _rows.Add(row);

        public void AddCategory(PlannedCategory row) => _categories.Add(row);

        public MenuImportReport Report(bool committed)
        {
            var rows = _rows.Select(r => new MenuImportRow(
                r.RowNumber,
                r.ExistingSlug ?? CreatedSlugs.GetValueOrDefault(r.RowNumber) ?? r.Request.Slug,
                r.Request.NameVi,
                r.ExistingSlug is null ? MenuImportAction.Create : MenuImportAction.Update,
                r.Errors)).ToArray();
            var failed = rows.Count(r => r.Errors.Count > 0);
            return new MenuImportReport(
                committed,
                rows.Length,
                rows.Count(r => r.Errors.Count == 0 && r.Action == MenuImportAction.Create),
                rows.Count(r => r.Errors.Count == 0 && r.Action == MenuImportAction.Update),
                failed,
                rows,
                _categories.Select(c => new MenuImportRow(
                    c.RowNumber,
                    c.ExistingSlug ?? CreatedCategorySlugs.GetValueOrDefault(c.RowNumber) ?? c.Slug,
                    c.Parsed.Name,
                    c.ExistingSlug is null ? MenuImportAction.Create : MenuImportAction.Update,
                    c.Errors)).ToArray());
        }
    }
}
