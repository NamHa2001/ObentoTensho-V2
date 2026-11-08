using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ObentoTensho.Core.Audit.Domain;

namespace ObentoTensho.Api.Logging;

// SEC-53: log không chứa mật khẩu, token, OTP, vé hub, cookie, Authorization; email/SĐT che phần giữa;
// bỏ ký tự xuống dòng/điều khiển trong giá trị để người dùng không chèn được dòng log giả (log injection)
public static partial class LogRedactor
{
    public const string Hidden = AuditRedactor.Hidden;

    private static readonly string[] SecretMarkers =
        ["password", "token", "secret", "otp", "totp", "authorization", "cookie", "ticket", "apikey", "api_key", "recoverycode", "stamp", "hash"];

    // Khóa chứa một trong các dấu hiệu bí mật thì cả giá trị bị ẩn
    public static bool IsSecretKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var lower = key.ToLowerInvariant();
        return SecretMarkers.Any(m => lower.Contains(m, StringComparison.Ordinal));
    }

    public static string Value(string key, object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return IsSecretKey(key) ? Hidden : Scrub(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
    }

    // Che theo mẫu trong chuỗi tự do (thông điệp, đường dẫn, ngoại lệ)
    public static string Scrub(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return text;
        }

        var result = SecretQuery().Replace(text, m => m.Groups["name"].Value + Hidden);
        result = Bearer().Replace(result, "Bearer " + Hidden);
        result = Email().Replace(result, m => AuditRedactor.MaskEmail(m.Value));
        result = Phone().Replace(result, m => AuditRedactor.MaskPhone(m.Value));
        return StripControl(result);
    }

    private static string StripControl(string text)
    {
        if (!text.Any(char.IsControl))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(char.IsControl(c) ? ' ' : c);
        }

        return builder.ToString();
    }

    // ?ticket=…, &token=…, #token=… (liên kết trong email), access_token của SignalR
    [GeneratedRegex(@"(?<name>[?&#](?:ticket|token|access_token|code|key|signature|otp)=)[^&\s""'#]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SecretQuery();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-_\.=+/]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Bearer();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Email();

    // SĐT Việt Nam dạng 0xxxxxxxxx hoặc +84xxxxxxxxx, không nằm trong chuỗi số dài hơn (mã đơn, số tiền, mã băm)
    [GeneratedRegex(@"(?<![\w+])(?:\+84|0)\d{9}(?!\w)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Phone();
}
