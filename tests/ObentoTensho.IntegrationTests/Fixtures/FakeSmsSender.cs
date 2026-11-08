using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ObentoTensho.Core.Verification.Abstractions;

namespace ObentoTensho.IntegrationTests.Fixtures;

public sealed partial class FakeSmsSender : ISmsSender
{
    private readonly ConcurrentQueue<(string Phone, string Message)> _sent = new();

    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        _sent.Enqueue((phoneNumber, message));
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> To(string phoneNumber) => _sent.Where(m => string.Equals(m.Phone, phoneNumber, StringComparison.Ordinal)).Select(m => m.Message).ToArray();

    // Mã 6 số trong tin mới nhất gửi tới số này
    public string LastCode(string phoneNumber) => Code().Match(To(phoneNumber)[^1]).Value;

    [GeneratedRegex(@"\b\d{6}\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Code();
}
