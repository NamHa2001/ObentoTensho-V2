using FluentValidation;
using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Core.Orders.Services;
using ObentoTensho.Core.Orders.Validators;

namespace ObentoTensho.Api.Configuration;

// Đặt món mang về (docs/srs/12)
public static class OrdersConfiguration
{
    public static IServiceCollection AddOrderServices(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();
        services.AddSingleton<IValidator<QuoteOrderRequest>, QuoteOrderRequestValidator>();

        services.AddScoped<OrderCart>();
        services.AddScoped<OrderPickup>();
        services.AddScoped<OrderTransitions>();
        services.AddScoped<PublicOrderService>();
        services.AddScoped<OrderDeskService>();
        return services;
    }
}
