using System.Globalization;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Notifications.Abstractions;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Infrastructure.Email;

// Máy dev: ghi thư thành tệp .eml (mở bằng Outlook/Thunderbird để xem) thay vì gửi thật
internal sealed class PickupDirectoryEmailSender(IOptions<EmailOptions> options, IClock clock) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var directory = Path.GetFullPath(settings.PickupDirectory);
        Directory.CreateDirectory(directory);

        var name = $"{clock.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{Guid.NewGuid():N}.eml";
        await using var file = File.Create(Path.Combine(directory, name));
        await MimeMessageFactory.Create(message, settings).WriteToAsync(file, cancellationToken);
    }
}
