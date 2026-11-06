namespace ObentoTensho.Api.Configuration;

// SEC-41: origin frontend được phép gọi API (CORS) và gửi cookie refresh (SEC-42); khách và quản trị tách riêng
public sealed class WebOriginsOptions
{
    public const string SectionName = "Web";

    public IList<string> CustomerOrigins { get; } = [];

    public IList<string> AdminOrigins { get; } = [];

    public string? Validate()
    {
        if (CustomerOrigins.Count == 0 || AdminOrigins.Count == 0)
        {
            return "Thiếu Web:CustomerOrigins hoặc Web:AdminOrigins.";
        }

        if (CustomerOrigins.Intersect(AdminOrigins, StringComparer.OrdinalIgnoreCase).Any())
        {
            return "Origin khách và quản trị phải khác nhau.";
        }

        var invalid = CustomerOrigins.Concat(AdminOrigins).FirstOrDefault(o => !IsOrigin(o));
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
