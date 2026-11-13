using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Delivery.Abstractions;
using ObentoTensho.Core.Notifications.Abstractions;
using ObentoTensho.Core.Notifications.Services;
using ObentoTensho.Core.Verification.Abstractions;
using ObentoTensho.Infrastructure.Delivery;
using ObentoTensho.Infrastructure.Email;
using ObentoTensho.Infrastructure.Jobs;
using ObentoTensho.Infrastructure.Messaging;
using ObentoTensho.Infrastructure.Persistence.Repositories;
using ObentoTensho.Infrastructure.Reservations;
using ObentoTensho.Infrastructure.Security;
using ObentoTensho.Infrastructure.Settings;

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
        services.AddSingleton<INotificationSettings, StoreNotificationSettings>();
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

        // OTP (BR-AUTH-18)
        services.AddSingleton<IOtpSettings, StoreOtpSettings>();
        services.AddScoped<IOtpRepository, OtpRepository>();
        services.AddScoped<IOtpEmailOutbox, OtpEmailOutbox>();
        services.AddOptions<SmsOptions>()
            .Bind(configuration.GetSection(SmsOptions.SectionName))
            .Validate(o => o.Validate(isProduction) is null, "Cấu hình SMS không hợp lệ (xem SmsOptions.Validate).")
            .ValidateOnStart();
        services.AddSingleton<ISmsSender, LogSmsSender>();

        // Turnstile (SEC-22, SEC-57) — chờ tối đa 5 giây rồi từ chối
        services.AddOptions<TurnstileOptions>()
            .Bind(configuration.GetSection(TurnstileOptions.SectionName))
            .Validate(o => o.Validate(isProduction) is null, "Cấu hình Turnstile không hợp lệ (xem TurnstileOptions.Validate).")
            .ValidateOnStart();
        services.AddHttpClient<ITurnstileVerifier, CloudflareTurnstileVerifier>(c => c.Timeout = TimeSpan.FromSeconds(5));

        // Giao hàng: quãng đường qua Google Routes; chưa có khóa thì báo chưa tính được phí
        services.AddOptions<MapsOptions>().Bind(configuration.GetSection(MapsOptions.SectionName));
        services.AddHttpClient<IRouteDistance, GoogleRouteDistance>(c => c.Timeout = TimeSpan.FromSeconds(8));

        // Việc định kỳ (docs/srs/20 §4)
        services.AddOptions<JobOptions>()
            .Bind(configuration.GetSection(JobOptions.SectionName))
            .Validate(o => o.TickSeconds is >= 5 and <= 300, "Jobs:TickSeconds phải từ 5 đến 300.")
            .ValidateOnStart();
        services.AddSingleton<SqlJobLock>();
        services.AddScoped<IJobRunStore, JobRunStore>();
        services.AddHostedService<RecurringJobWorker>();
        return services;
    }
}
