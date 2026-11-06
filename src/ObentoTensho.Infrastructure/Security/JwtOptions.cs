using System.Buffers.Text;
using Microsoft.IdentityModel.Tokens;

namespace ObentoTensho.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    // SEC-16: khóa HS256 tối thiểu 256 bit
    public const int MinimumKeyBytes = 32;

    // BR-AUTH-02, TIME-13
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    public string Issuer { get; set; } = "obento-tensho";

    public string CustomerAudience { get; set; } = "obento-customer";

    public int AccessTokenMinutes { get; set; } = 15;

    // SEC-50: khóa luân phiên theo "kid"; ký bằng khóa đang dùng, xác minh bằng mọi khóa còn trong danh sách
    public string ActiveKeyId { get; set; } = string.Empty;

    public IDictionary<string, string> SigningKeys { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public SecurityKey GetActiveKey() => CreateKey(ActiveKeyId, SigningKeys[ActiveKeyId]);

    public IEnumerable<SecurityKey> GetValidationKeys() => SigningKeys.Select(k => CreateKey(k.Key, k.Value));

    // Trả null khi hợp lệ; dùng cho ValidateOnStart để thiếu/yếu khóa thì app không chạy
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(CustomerAudience))
        {
            return "Thiếu Jwt:Issuer hoặc Jwt:CustomerAudience.";
        }

        if (AccessTokenMinutes is < 1 or > 60)
        {
            return "Jwt:AccessTokenMinutes phải từ 1 đến 60.";
        }

        if (string.IsNullOrWhiteSpace(ActiveKeyId) || !SigningKeys.ContainsKey(ActiveKeyId))
        {
            return "Jwt:ActiveKeyId phải trỏ tới một khóa trong Jwt:SigningKeys.";
        }

        var weak = SigningKeys.FirstOrDefault(k => !IsStrong(k.Value));
        return weak.Key is null ? null : $"Khóa ký JWT '{weak.Key}' không hợp lệ hoặc yếu (cần Base64 ngẫu nhiên ≥ {MinimumKeyBytes} byte).";
    }

    private static SymmetricSecurityKey CreateKey(string keyId, string base64) =>
        new(Convert.FromBase64String(base64)) { KeyId = keyId };

    // Chặn khóa mẫu kiểu toàn số 0 / lặp một ký tự
    private static bool IsStrong(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64) || !Base64.IsValid(base64, out var length) || length < MinimumKeyBytes)
        {
            return false;
        }

        return Convert.FromBase64String(base64).Distinct().Count() >= MinimumKeyBytes / 2;
    }
}
