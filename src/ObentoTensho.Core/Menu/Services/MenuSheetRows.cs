using System.Globalization;
using System.Text.RegularExpressions;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Core.Menu.Services;

// Chuyển dòng Excel ↔ dữ liệu món (BR-MNU-14). Chỉ đọc kiểu dữ liệu; quy tắc nghiệp vụ do validator của form tạo món kiểm.
public static partial class MenuSheetRows
{
    public sealed record Parsed(
        string? Slug,
        string? CategorySlug,
        string Name,
        string? ShortDesc,
        decimal Price,
        decimal? OriginalPrice,
        SaleStatus Status,
        bool AllowTakeaway,
        MenuLabels Labels,
        IReadOnlyList<Allergen> Allergens,
        int? DisplayOrder);

    private static readonly Dictionary<string, SaleStatus> Statuses = new(StringComparer.Ordinal)
    {
        ["dang ban"] = SaleStatus.Available,
        ["available"] = SaleStatus.Available,
        ["het hang"] = SaleStatus.SoldOut,
        ["sold_out"] = SaleStatus.SoldOut,
        ["an"] = SaleStatus.Hidden,
        ["hidden"] = SaleStatus.Hidden,
    };

    private static readonly HashSet<string> Truthy = new(["co", "x", "1", "true", "yes"], StringComparer.Ordinal);
    private static readonly HashSet<string> Falsy = new(["khong", "0", "false", "no"], StringComparer.Ordinal);

    public static Parsed Parse(MenuSheetRow row, ICollection<MenuImportError> errors)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(errors);
        foreach (var column in MenuSheet.Columns.Where(row.FormulaColumns.Contains))
        {
            errors.Add(new MenuImportError(column, FieldErrorCodes.InvalidFormat));
        }

        bool Flag(string column, bool whenEmpty) => ParseFlag(row, column, whenEmpty, errors);

        return new Parsed(
            row[MenuSheet.Slug],
            row[MenuSheet.Category],
            row[MenuSheet.Name] ?? string.Empty,
            row[MenuSheet.ShortDesc],
            ParseMoney(row, MenuSheet.Price, errors) ?? 0,
            ParseMoney(row, MenuSheet.OriginalPrice, errors),
            ParseStatus(row, errors),
            Flag(MenuSheet.Takeaway, whenEmpty: true),
            new MenuLabels(Flag(MenuSheet.IsNew, false), Flag(MenuSheet.Bestseller, false), Flag(MenuSheet.Featured, false), Flag(MenuSheet.Vegetarian, false), Flag(MenuSheet.Spicy, false)),
            ParseAllergens(row, errors),
            ParseOrder(row, errors));
    }

    // Một dòng xuất, cùng thứ tự MenuSheet.Columns; xuất rồi nhập lại không đổi gì
    public static IReadOnlyList<object?> ToRow(MenuItem item, string categorySlug)
    {
        ArgumentNullException.ThrowIfNull(item);
        return
        [
            item.Slug, categorySlug, item.NameVi, item.ShortDesc, item.Price, item.OriginalPrice, StatusText(item.Status),
            YesNo(item.AllowTakeaway), YesNo(item.IsNew), YesNo(item.IsBestseller), YesNo(item.IsFeatured), YesNo(item.IsVegetarian), YesNo(item.IsSpicy),
            item.Allergens, item.DisplayOrder,
        ];
    }

    private static string StatusText(SaleStatus status) => status switch
    {
        SaleStatus.Available => "Đang bán",
        SaleStatus.SoldOut => "Hết hàng",
        _ => "Ẩn",
    };

    private static string YesNo(bool value) => value ? MenuSheet.Yes : MenuSheet.No;

    // Nhận 65000, 65000.0 (ô số) và 65.000 / 65,000 / 65 000 (ô chữ, dấu phân cách hàng nghìn)
    private static decimal? ParseMoney(MenuSheetRow row, string column, ICollection<MenuImportError> errors)
    {
        if (row[column] is not { } text)
        {
            return null;
        }

        var plain = ThousandsPattern().IsMatch(text) ? text.Replace(".", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal) : text;
        if (decimal.TryParse(plain, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add(new MenuImportError(column, FieldErrorCodes.InvalidFormat));
        return null;
    }

    private static SaleStatus ParseStatus(MenuSheetRow row, ICollection<MenuImportError> errors)
    {
        if (row[MenuSheet.Status] is not { } text)
        {
            return SaleStatus.Available;
        }

        if (Statuses.TryGetValue(TextNormalizer.Search(text), out var status))
        {
            return status;
        }

        errors.Add(new MenuImportError(MenuSheet.Status, FieldErrorCodes.InvalidFormat));
        return SaleStatus.Available;
    }

    private static bool ParseFlag(MenuSheetRow row, string column, bool whenEmpty, ICollection<MenuImportError> errors)
    {
        if (row[column] is not { } text)
        {
            return whenEmpty;
        }

        var key = TextNormalizer.Search(text);
        if (Truthy.Contains(key) || Falsy.Contains(key))
        {
            return Truthy.Contains(key);
        }

        errors.Add(new MenuImportError(column, FieldErrorCodes.InvalidFormat));
        return whenEmpty;
    }

    // Khác tham số lọc công khai: mã lạ trong tệp nhập là lỗi, không bỏ qua
    private static IReadOnlyList<Allergen> ParseAllergens(MenuSheetRow row, ICollection<MenuImportError> errors)
    {
        if (row[MenuSheet.Allergens] is not { } text)
        {
            return [];
        }

        var codes = text.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var allergens = MenuRules.ParseAllergens(string.Join(',', codes));
        if (codes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != allergens.Count)
        {
            errors.Add(new MenuImportError(MenuSheet.Allergens, FieldErrorCodes.InvalidFormat));
        }

        return allergens;
    }

    private static int? ParseOrder(MenuSheetRow row, ICollection<MenuImportError> errors)
    {
        if (row[MenuSheet.DisplayOrder] is not { } text)
        {
            return null;
        }

        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var order) && order <= 100_000)
        {
            return order;
        }

        errors.Add(new MenuImportError(MenuSheet.DisplayOrder, FieldErrorCodes.InvalidFormat));
        return null;
    }

    [GeneratedRegex(@"^\d{1,3}([., ]\d{3})+$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex ThousandsPattern();
}
