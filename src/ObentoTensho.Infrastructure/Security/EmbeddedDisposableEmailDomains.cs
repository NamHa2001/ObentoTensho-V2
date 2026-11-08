using System.Collections.Frozen;
using ObentoTensho.Core.Auth.Abstractions;

namespace ObentoTensho.Infrastructure.Security;

// Danh sách nằm trong tệp disposable-email-domains.txt (mỗi dòng một tên miền, # là chú thích): sửa tệp là đổi được,
// không cần sửa mã. Tên miền con của tên miền trong danh sách cũng bị coi là dùng một lần
internal sealed class EmbeddedDisposableEmailDomains : IDisposableEmailDomains
{
    private const string ResourceName = "ObentoTensho.Infrastructure.Security.Resources.disposable-email-domains.txt";

    private static readonly Lazy<FrozenSet<string>> Domains = new(Load);

    public static int Count => Domains.Value.Count;

    public bool IsDisposable(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        var at = email.LastIndexOf('@');
        if (at < 0)
        {
            return false;
        }

        var domain = email[(at + 1)..].Trim().TrimEnd('.').ToLowerInvariant();
        while (domain.Length > 0)
        {
            if (Domains.Value.Contains(domain))
            {
                return true;
            }

            var dot = domain.IndexOf('.', StringComparison.Ordinal);
            domain = dot < 0 ? string.Empty : domain[(dot + 1)..];
        }

        return false;
    }

    private static FrozenSet<string> Load()
    {
        using var stream = typeof(EmbeddedDisposableEmailDomains).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Thiếu tài nguyên {ResourceName}.");
        using var reader = new StreamReader(stream);
        var domains = new HashSet<string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            var value = line.Trim().ToLowerInvariant();
            if (value.Length > 0 && !value.StartsWith('#'))
            {
                domains.Add(value);
            }
        }

        return domains.ToFrozenSet(StringComparer.Ordinal);
    }
}
