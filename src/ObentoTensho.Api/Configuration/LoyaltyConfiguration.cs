using FluentValidation;
using ObentoTensho.Contracts.Dtos.Loyalty;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Loyalty.Services;
using ObentoTensho.Core.Loyalty.Validators;

namespace ObentoTensho.Api.Configuration;

// Điểm thưởng và hạng thành viên (docs/srs/13)
public static class LoyaltyConfiguration
{
    public static IServiceCollection AddLoyaltyServices(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<AdjustPointsRequest>, AdjustPointsRequestValidator>();
        services.AddScoped<LoyaltyLedger>();
        services.AddScoped<LoyaltyService>();

        // Việc định kỳ (docs/srs/20 §4)
        services.AddScoped<LoyaltyJobs>();
        services.AddScoped<IRecurringJob, LoyaltyExpiryJob>();
        services.AddScoped<IRecurringJob, LoyaltyReconcileJob>();
        services.AddScoped<IRecurringJob, ObentoTensho.Core.Notifications.Services.EmailQuotaAlertJob>();
        services.AddScoped<IRecurringJob, LoyaltyExpiryNoticeJob>();
        return services;
    }
}
