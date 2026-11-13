using System.Text.RegularExpressions;

namespace ObentoTensho.Core.Invoices.Domain;

// CCCD: 12 chữ số. MST: 10 chữ số, đơn vị phụ thuộc 10 số + "-" + 3 số hoặc viết liền 13 số
public static partial class InvoiceRules
{
    public const int NameMaxLength = 200;
    public const int AddressMaxLength = 300;
    public const int NoteMaxLength = 300;
    public const int InvoiceNumberMaxLength = 50;

    public static bool IsNationalId(string? value) => value is not null && NationalIdPattern().IsMatch(value);

    public static bool IsTaxCode(string? value) => value is not null && TaxCodePattern().IsMatch(value);

    // Số CCCD chỉ hiện 3 số cuối
    public static string? MaskNationalId(string? value) =>
        value is { Length: > 3 } ? new string('*', value.Length - 3) + value[^3..] : value;

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^\d{12}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex NationalIdPattern();

    [GeneratedRegex(@"^\d{10}(-?\d{3})?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex TaxCodePattern();
}
