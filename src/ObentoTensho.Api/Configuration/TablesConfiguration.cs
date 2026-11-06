using FluentValidation;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Core.Tables.Services;
using ObentoTensho.Core.Tables.Validators;

namespace ObentoTensho.Api.Configuration;

// Bàn, khu vực, ca phục vụ, khóa ngày, giờ mở cửa (docs/srs/16)
public static class TablesConfiguration
{
    public static IServiceCollection AddTablesServices(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<TableAreaRequest>, TableAreaRequestValidator>();
        services.AddSingleton<IValidator<TableRequest>, TableRequestValidator>();
        services.AddSingleton<IValidator<WeeklyHoursRequest>, WeeklyHoursRequestValidator>();
        services.AddSingleton<IValidator<SpecialHoursRequest>, SpecialHoursRequestValidator>();
        services.AddSingleton<IValidator<ServiceSlotRequest>, ServiceSlotRequestValidator>();
        services.AddSingleton<IValidator<BlockedDateRequest>, BlockedDateRequestValidator>();

        services.AddScoped<TableAreaService>();
        services.AddScoped<TableService>();
        services.AddScoped<OpeningHoursService>();
        services.AddScoped<ServiceSlotService>();
        services.AddScoped<BlockedDateService>();
        return services;
    }
}
