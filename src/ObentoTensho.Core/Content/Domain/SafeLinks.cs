using System.Text.RegularExpressions;

namespace ObentoTensho.Core.Content.Domain;

// SEC-47: liên kết CTA; BR-CNT-35: video gallery chỉ YouTube/TikTok, nhúng ở chế độ ít theo dõi
public static partial class SafeLinks
{
    private static readonly string[] YoutubeHosts = ["youtube.com", "www.youtube.com", "m.youtube.com", "youtu.be"];
    private static readonly string[] TiktokHosts = ["tiktok.com", "www.tiktok.com"];

    // Đường dẫn nội bộ "/…" (không phải "//host", không có dấu gạch ngược), hoặc https thuộc danh sách miền cho phép
    public static bool IsAllowedCta(string link, IReadOnlyCollection<string> allowedHosts)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(allowedHosts);
        if (link.Contains('\\', StringComparison.Ordinal) || link.Any(char.IsControl))
        {
            return false;
        }

        if (link.StartsWith('/'))
        {
            return !link.StartsWith("//", StringComparison.Ordinal);
        }

        return Uri.TryCreate(link, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            && string.IsNullOrEmpty(uri.UserInfo)
            && allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
    }

    // Địa chỉ nhúng của video; null nếu không phải liên kết YouTube/TikTok hợp lệ
    public static string? VideoEmbedUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return null;
        }

        if (YoutubeHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            var id = string.Equals(uri.Host, "youtu.be", StringComparison.OrdinalIgnoreCase)
                ? uri.AbsolutePath.TrimStart('/')
                : YoutubeId(uri);
            return id is not null && YoutubeIdPattern().IsMatch(id) ? $"https://www.youtube-nocookie.com/embed/{id}" : null;
        }

        if (TiktokHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            var match = TiktokPathPattern().Match(uri.AbsolutePath);
            return match.Success ? $"https://www.tiktok.com/embed/v2/{match.Groups["id"].Value}" : null;
        }

        return null;
    }

    private static string? YoutubeId(Uri uri)
    {
        var path = uri.AbsolutePath;
        if (path.StartsWith("/shorts/", StringComparison.Ordinal) || path.StartsWith("/embed/", StringComparison.Ordinal))
        {
            return path[(path.IndexOf('/', 1) + 1)..];
        }

        return string.Equals(path, "/watch", StringComparison.Ordinal)
            ? uri.Query.TrimStart('?').Split('&').FirstOrDefault(p => p.StartsWith("v=", StringComparison.Ordinal))?[2..]
            : null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{6,20}$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex YoutubeIdPattern();

    [GeneratedRegex("^/@[^/]+/video/(?<id>[0-9]{5,25})/?$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex TiktokPathPattern();
}
