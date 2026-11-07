using System.Globalization;
using System.Text;

namespace ObentoTensho.Core.Common;

// Bỏ dấu tiếng Việt cho slug (BR-MNU-04) và chuỗi tìm kiếm (DB-17): "Cơm gà Hội An" → "com-ga-hoi-an" / "com ga hoi an"
public static class TextNormalizer
{
    public static string Slug(string text, int maxLength = 150)
    {
        var plain = RemoveDiacritics(text).ToLowerInvariant();
        var builder = new StringBuilder(plain.Length);
        foreach (var c in plain)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length <= maxLength ? slug : slug[..maxLength].TrimEnd('-');
    }

    public static string Search(params string?[] texts) =>
        string.Join(' ', texts
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => string.Join(' ', RemoveDiacritics(t ?? string.Empty).ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))));

    public static string RemoveDiacritics(string text)
    {
        var decomposed = text.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        {
            builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
