using FluentValidation;
using ObentoTensho.Contracts.Dtos.Promotions;
using ObentoTensho.Contracts.Dtos.Vouchers;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Promotions.Services;
using ObentoTensho.Core.Promotions.Validators;
using ObentoTensho.Core.Vouchers.Services;
using ObentoTensho.Core.Vouchers.Validators;

namespace ObentoTensho.Api.Configuration;

// Voucher và khuyến mãi (docs/srs/14)
public static class VoucherConfiguration
{
    public static IServiceCollection AddVoucherServices(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<VoucherRequest>, VoucherRequestValidator>();
        services.AddSingleton<IValidator<BulkGenerateVouchersRequest>, BulkGenerateVouchersRequestValidator>();
        services.AddSingleton<IValidator<ValidateVoucherRequest>, ValidateVoucherRequestValidator>();
        services.AddScoped<VoucherEvaluator>();
        services.AddScoped<VoucherService>();
        services.AddScoped<VoucherAdminService>();
        services.AddScoped<BirthdayVoucherService>();
        services.AddScoped<IRecurringJob, VoucherBirthdayJob>();

        services.AddSingleton<IValidator<PromotionRequest>, PromotionRequestValidator>();
        services.AddScoped<PublicPromotionService>();
        services.AddScoped<PromotionAdminService>();
        return services;
    }
}
