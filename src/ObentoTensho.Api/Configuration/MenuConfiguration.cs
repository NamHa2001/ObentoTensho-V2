using FluentValidation;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Core.Menu.Services;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.Api.Configuration;

// Thực đơn (docs/srs/15)
public static class MenuConfiguration
{
    public static IServiceCollection AddMenuServices(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<CategoryRequest>, CategoryRequestValidator>();
        services.AddSingleton<IValidator<MenuItemRequest>, MenuItemRequestValidator>();
        services.AddSingleton<IValidator<OptionGroupRequest>, OptionGroupRequestValidator>();
        services.AddSingleton<IValidator<OptionRequest>, OptionRequestValidator>();
        services.AddSingleton<IValidator<TranslationRequest>, TranslationRequestValidator>();

        services.AddScoped<PublicMenuService>();
        services.AddScoped<MenuCategoryAdminService>();
        services.AddScoped<MenuItemAdminService>();
        services.AddScoped<OptionGroupAdminService>();
        services.AddScoped<MenuTranslationService>();
        return services;
    }
}
