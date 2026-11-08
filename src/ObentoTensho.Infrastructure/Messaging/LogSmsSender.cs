using Microsoft.Extensions.Logging;
using ObentoTensho.Core.Verification.Abstractions;

namespace ObentoTensho.Infrastructure.Messaging;

// Chỉ dùng khi phát triển: in nội dung SMS ra log để thử luồng xác minh mà không gửi thật. Production bị chặn ở SmsOptions
internal sealed partial class LogSmsSender(ILogger<LogSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        LogSms(logger, phoneNumber.Length > 4 ? phoneNumber[^4..] : phoneNumber, message);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS (không gửi thật) tới số đuôi {PhoneLast4}: {Message}")]
    private static partial void LogSms(ILogger logger, string phoneLast4, string message);
}
