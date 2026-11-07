using FluentValidation;
using ObentoTensho.Contracts.Dtos.Vouchers;
using ObentoTensho.Core.Vouchers.Services;
using ObentoTensho.Core.Vouchers.Validators;

namespace ObentoTensho.Api.Configuration;

// Voucher (docs/srs/14)
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
        return services;
    }
}
