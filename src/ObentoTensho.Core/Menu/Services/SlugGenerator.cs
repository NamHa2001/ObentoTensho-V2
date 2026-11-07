using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Menu.Services;

// BR-MNU-04, BR-MNU-12: slug tự sinh từ tên không dấu; trùng thì thêm hậu tố -2, -3…
public static class SlugGenerator
{
    private const int MaxAttempts = 50;

    public static async Task<string> UniqueAsync(string? requested, string name, int maxLength, Func<string, Task<bool>> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        var baseSlug = string.IsNullOrWhiteSpace(requested) ? TextNormalizer.Slug(name, maxLength - 4) : requested.Trim();
        if (baseSlug.Length == 0)
        {
            baseSlug = "mon";
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var candidate = attempt == 1 ? baseSlug : $"{baseSlug}-{attempt}";
            if (!await exists(candidate))
            {
                return candidate;
            }
        }

        return $"{baseSlug}-{Guid.NewGuid():N}"[..maxLength];
    }
}
