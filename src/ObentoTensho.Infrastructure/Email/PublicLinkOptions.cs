namespace ObentoTensho.Infrastructure.Email;

// SEC-66: liên kết trong email dựng từ cấu hình, không bao giờ từ header Host/X-Forwarded-Host
public sealed class PublicLinkOptions
{
    public const string SectionName = "Web";

    public string PublicBaseUrl { get; set; } = string.Empty;

    public string AdminBaseUrl { get; set; } = string.Empty;

    // SEC-60: email nhận báo lỗ hổng cho /.well-known/security.txt; để trống thì không phục vụ tệp này
    public string? SecurityContact { get; set; }

    public string? Validate() =>
        IsBaseUrl(PublicBaseUrl) && IsBaseUrl(AdminBaseUrl)
            ? null
            : "Web:PublicBaseUrl / Web:AdminBaseUrl phải là https://ten-mien (http chỉ cho localhost).";

    private static bool IsBaseUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && uri.IsLoopback))
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
}
