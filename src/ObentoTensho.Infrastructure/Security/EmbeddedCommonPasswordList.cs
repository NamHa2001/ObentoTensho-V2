using System.Collections.Frozen;
using ObentoTensho.Core.Auth.Abstractions;

namespace ObentoTensho.Infrastructure.Security;

// Danh sách nhúng trong assembly, nạp một lần; so sánh không phân biệt hoa thường
internal sealed class EmbeddedCommonPasswordList : ICommonPasswordList
{
    private const string ResourceName = "ObentoTensho.Infrastructure.Security.Resources.common-passwords.txt";

    private static readonly Lazy<FrozenSet<string>> Passwords = new(Load);

    public static int Count => Passwords.Value.Count;

    public bool Contains(string password) => Passwords.Value.Contains(password);

    private static FrozenSet<string> Load()
    {
        using var stream = typeof(EmbeddedCommonPasswordList).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Thiếu tài nguyên {ResourceName}.");
        using var reader = new StreamReader(stream);

        var passwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            var value = line.Trim();
            if (value.Length > 0 && !value.StartsWith('#'))
            {
                passwords.Add(value);
            }
        }

        return passwords.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }
}
