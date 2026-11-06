using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Notifications.Abstractions;
using ObentoTensho.Core.Notifications.Services;
using ObentoTensho.Infrastructure.Email;
using ObentoTensho.Infrastructure.Messaging;
using ObentoTensho.Infrastructure.Persistence.Repositories;

namespace ObentoTensho.Infrastructure;

public static class MessagingRegistration
{
    // Email + hộp thư đi (docs/srs/20). Thiếu/ sai cấu hình thì dừng khi khởi động.
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration, bool isProduction)
    {
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(o => o.Validate(isProduction) is null, "Cấu hình Email không hợp lệ (xem EmailOptions.Validate).")
            .ValidateOnStart();
        services.AddOptions<PublicLinkOptions>()
            .Bind(configuration.GetSection(PublicLinkOptions.SectionName))
            .Validate(o => o.Validate() is null, "Cấu hình Web:PublicBaseUrl / Web:AdminBaseUrl không hợp lệ.")
            .ValidateOnStart();
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .Validate(o => o.BatchSize is >= 1 and <= 500 && o.MaxAttempts is >= 1 and <= 20, "Cấu hình Outbox không hợp lệ.")
            .ValidateOnStart();

        services.AddSingleton<IEmailTemplateRenderer, EmbeddedEmailTemplateRenderer>();
        services.AddSingleton<INotificationSettings, OptionsNotificationSettings>();
        services.AddSingleton<SmtpEmailSender>();
        services.AddSingleton<PickupDirectoryEmailSender>();
        services.AddSingleton<IEmailSender>(sp =>
            string.Equals(sp.GetRequiredService<IOptions<EmailOptions>>().Value.Mode, EmailOptions.SmtpMode, StringComparison.Ordinal)
                ? sp.GetRequiredService<SmtpEmailSender>()
                : sp.GetRequiredService<PickupDirectoryEmailSender>());

        services.AddScoped<IEmailLogRepository, EmailLogRepository>();
        services.AddScoped<EmailDispatchService>();
        services.AddScoped<OutboxStore>();
        services.AddSingleton<OutboxProcessor>();
        services.AddHostedService<OutboxWorker>();
        return services;
    }
}
