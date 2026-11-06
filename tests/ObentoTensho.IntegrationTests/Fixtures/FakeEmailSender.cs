using System.Collections.Concurrent;
using ObentoTensho.Core.Notifications.Abstractions;
using ObentoTensho.Core.Notifications.Domain;

namespace ObentoTensho.IntegrationTests.Fixtures;

// Thay nhà cung cấp email trong test: giữ lại thư đã "gửi", có thể giả lập lỗi SMTP
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public bool Fail { get; set; }

    public IReadOnlyList<EmailMessage> Sent => _sent.ToArray();

    public IReadOnlyList<EmailMessage> To(string email) =>
        _sent.Where(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase)).ToArray();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new IOException("SMTP giả lập không phản hồi.");
        }

        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
