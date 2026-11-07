using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Menu.Domain;

// Quy tắc nhỏ dùng chung của thực đơn (docs/srs/15)
public static class MenuRules
{
    public const decimal MaxPrice = 100_000_000m;
    public const decimal MaxPriceDelta = 10_000_000m;
    public const int MaxGroupsPerItem = 10;
    public const int MaxOptionsPerGroup = 20;

    // BR-MNU-16: hết hàng có hạn ⇒ sau mốc đó tự coi là đang bán
    public static SaleStatus Effective(SaleStatus status, DateTime? soldOutUntilUtc, DateTime nowUtc) =>
        status == SaleStatus.SoldOut && soldOutUntilUtc is { } until && until <= nowUtc ? SaleStatus.Available : status;

    public static string? JoinAllergens(IEnumerable<Allergen>? allergens)
    {
        var codes = (allergens ?? []).Distinct().Order().Select(Code).ToArray();
        return codes.Length == 0 ? null : string.Join(',', codes);
    }

    // Dùng cả cho tham số ?excludeAllergens= của khách nên bỏ qua mã lạ thay vì ném lỗi
    public static IReadOnlyList<Allergen> ParseAllergens(string? codes) =>
        string.IsNullOrEmpty(codes)
            ? []
            : Enum.GetValues<Allergen>()
                .Where(a => codes.Split(',', StringSplitOptions.TrimEntries).Contains(Code(a), StringComparer.OrdinalIgnoreCase))
                .ToArray();

    public static string Code(Allergen allergen) => JsonNamingPolicy.SnakeCaseLower.ConvertName(allergen.ToString());

    // BR-I18N-02: băm các trường văn bản của bản Việt; đổi bản Việt ⇒ bản dịch cũ thành lỗi thời
    public static string SourceHash(params string?[] fields) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', fields.Select(f => f ?? string.Empty)))));
}
