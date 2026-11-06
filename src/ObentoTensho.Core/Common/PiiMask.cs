namespace ObentoTensho.Core.Common;

// SEC-01 d: che SĐT/email khi trả công khai hoặc cho vai trò không cần thấy đầy đủ
public static class PiiMask
{
    // "+84912345678" → "091****678"
    public static string Phone(string phone)
    {
        var national = phone.StartsWith("+84", StringComparison.Ordinal) ? "0" + phone[3..] : phone;
        return national.Length <= 6 ? "****" : $"{national[..3]}{new string('*', national.Length - 6)}{national[^3..]}";
    }

    // "nguyen.an@gmail.com" → "n***@gmail.com"
    public static string Email(string email)
    {
        var at = email.LastIndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }

    public static string Last4(string phone) => phone.Length >= 4 ? phone[^4..] : phone;
}
