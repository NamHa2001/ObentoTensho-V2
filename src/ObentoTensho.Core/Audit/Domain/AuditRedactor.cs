using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

namespace ObentoTensho.Core.Audit.Domain;

// BR-STF-10, SEC-53: bí mật thay bằng "***"; email/SĐT chỉ giữ dạng che
public static class AuditRedactor
{
    public const string Hidden = "***";

    // Giữ nguyên tiếng Việt/Nhật để người xem nhật ký đọc được; ký tự nhạy cảm HTML vẫn bị mã hóa
    private static readonly JsonSerializerOptions WriteOptions = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    private static readonly string[] SecretMarkers = ["password", "token", "secret", "hash", "stamp", "recoverycode", "otp", "totp"];

    public static string? ToJson(object? data)
    {
        if (data is null)
        {
            return null;
        }

        var node = JsonSerializer.SerializeToNode(data, JsonSerializerOptions.Web);
        Redact(node);
        return node?.ToJsonString(WriteOptions);
    }

    public static string MaskEmail(string email)
    {
        var at = email.LastIndexOf('@');
        return at <= 0 ? Hidden : $"{email[0]}{Hidden}{email[at..]}";
    }

    public static string MaskPhone(string phone) =>
        phone.Length <= 6 ? Hidden : $"{phone[..3]}{new string('*', phone.Length - 6)}{phone[^3..]}";

    private static void Redact(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    obj[key] = RedactProperty(key.ToLowerInvariant(), value);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Redact(item);
                }

                break;
        }
    }

    private static JsonNode? RedactProperty(string key, JsonNode? value)
    {
        if (value is null)
        {
            return null;
        }

        if (SecretMarkers.Any(m => key.Contains(m, StringComparison.Ordinal)))
        {
            return Hidden;
        }

        if (value is JsonValue v && v.TryGetValue<string>(out var text))
        {
            if (key.Contains("email", StringComparison.Ordinal))
            {
                return MaskEmail(text);
            }

            if (key.Contains("phone", StringComparison.Ordinal))
            {
                return MaskPhone(text);
            }
        }

        var copy = value.DeepClone();
        Redact(copy);
        return copy;
    }
}
