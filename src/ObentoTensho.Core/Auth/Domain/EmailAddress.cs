namespace ObentoTensho.Core.Auth.Domain;

public static class EmailAddress
{
    private static readonly string[] GmailDomains = ["gmail.com", "googlemail.com"];

    // Email lưu chữ thường (CK_Users_Email_Lowercase); so sánh không phân biệt hoa thường
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    // BR-AUTH-13: dạng chuẩn để kiểm trùng ưu đãi — bỏ "+tag", bỏ dấu chấm của Gmail
    public static string Canonicalize(string normalizedEmail)
    {
        var at = normalizedEmail.LastIndexOf('@');
        if (at <= 0)
        {
            return normalizedEmail;
        }

        var local = normalizedEmail[..at];
        var domain = normalizedEmail[(at + 1)..];

        var plus = local.IndexOf('+', StringComparison.Ordinal);
        if (plus > 0)
        {
            local = local[..plus];
        }

        if (GmailDomains.Contains(domain, StringComparer.Ordinal))
        {
            local = local.Replace(".", string.Empty, StringComparison.Ordinal);
            domain = GmailDomains[0];
        }

        return $"{local}@{domain}";
    }

    public static string LocalPart(string normalizedEmail)
    {
        var at = normalizedEmail.LastIndexOf('@');
        return at <= 0 ? normalizedEmail : normalizedEmail[..at];
    }
}
