namespace ObentoTensho.Api.Configuration;

// SEC-41: các origin frontend được phép gọi API (CORS) và gửi cookie refresh (SEC-42)
public sealed class WebOriginsOptions
{
    public const string SectionName = "Web";

    public IList<string> CustomerOrigins { get; } = [];

    public string? Validate()
    {
        if (CustomerOrigins.Count == 0)
        {
            return "Thiếu Web:CustomerOrigins.";
        }

        var invalid = CustomerOrigins.FirstOrDefault(o => !IsOrigin(o));
        return invalid is null ? null : $"Origin '{invalid}' không hợp lệ (dạng https://ten-mien, không có đường dẫn).";
    }

    // Chỉ cho http với localhost (máy dev)
    private static bool IsOrigin(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && uri.IsLoopback))
        && string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal)
        && !value.EndsWith('/')
        && string.IsNullOrEmpty(uri.Query);
}
