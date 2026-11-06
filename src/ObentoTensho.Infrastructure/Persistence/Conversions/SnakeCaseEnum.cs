using System.Text;

namespace ObentoTensho.Infrastructure.Persistence.Conversions;

// Lưu enum dạng chuỗi snake_case (vd ReuseDetected -> reuse_detected) theo docs/srs/08.
internal static class SnakeCaseEnum
{
    public static IReadOnlyList<string> AllValues<TEnum>()
        where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Select(ToSnakeCase).ToArray();

    public static string ToSnakeCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();
        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(name[i]));
        }

        return builder.ToString();
    }

    public static TEnum Parse<TEnum>(string text)
        where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().First(v => string.Equals(ToSnakeCase(v), text, StringComparison.Ordinal));
}
