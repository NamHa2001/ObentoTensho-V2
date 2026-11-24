using FluentValidation;
using ObentoTensho.Contracts.Dtos.Account;
using ObentoTensho.Contracts.Dtos.Customers;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Services;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Customers.Services;
using ObentoTensho.Core.Customers.Validators;
using ObentoTensho.Core.Reports.Services;
using ObentoTensho.Core.Settings.Services;

namespace ObentoTensho.Api.Configuration;

// Quản lý khách hàng và xuất dữ liệu (docs/srs/18 §1)
public static class CustomersConfiguration
{
    public static IServiceCollection AddCustomerServices(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<CustomerStatusRequest>, CustomerStatusRequestValidator>();
        services.AddSingleton<IValidator<DataExportRequest>, DataExportRequestValidator>();
        services.AddSingleton<IValidator<CustomerDeletionRequest>, CustomerDeletionRequestValidator>();
        services.AddSingleton<IValidator<UpdateProfileRequest>, UpdateProfileRequestValidator>();
        services.AddSingleton<IValidator<ChangeEmailRequest>, ChangeEmailRequestValidator>();
        services.AddScoped<AccountService>();
        services.AddScoped<PhoneVerificationService>();
        services.AddScoped<ContactVerificationPolicy>();
        services.AddScoped<AvatarService>();
        services.AddScoped<AccountDeletionService>();
        services.AddScoped<IRecurringJob, AccountDeletionJob>();
        services.AddScoped<BusinessCalendar>();
        services.AddScoped<StatsRollupService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ReportService>();
        services.AddScoped<PaymentGroupReportService>();
        services.AddScoped<IRecurringJob, StatsRollupJob>();
        services.AddScoped<IRecurringJob, StatsReconcileJob>();
        services.AddScoped<IRecurringJob, ObentoTensho.Core.Maintenance.Services.DataRetentionJob>();
        services.AddScoped<SettingsSeeder>();
        services.AddScoped<SettingsService>();
        services.AddScoped<CustomerAdminService>();
        services.AddScoped<DataExportService>();
        services.AddScoped<IDataExportSource, CustomerExportSource>();
        services.AddScoped<IDataExportSource, NewsletterExportSource>();
        services.AddScoped<DataExportJob>();
        services.AddScoped<IRecurringJob>(sp => sp.GetRequiredService<DataExportJob>());
        return services;
    }
}
