using FluentValidation;
using ObentoTensho.Contracts.Dtos.Invoices;
using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Delivery.Services;
using ObentoTensho.Core.Invoices.Services;
using ObentoTensho.Core.Invoices.Validators;
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
        services.AddScoped<DineInService>();
        services.AddScoped<PlatformOrderService>();
        services.AddScoped<OrderDeskService>();

        // Yêu cầu xuất hóa đơn đi kèm đơn (chủ quán 2026-11-13)
        services.AddSingleton<IValidator<InvoiceBuyerRequest>, InvoiceBuyerRequestValidator>();
        services.AddScoped<InvoiceService>();

        // Giao hàng (chủ quán 2026-11-13)
        services.AddScoped<DeliveryQuoteService>();

        // Việc định kỳ (docs/srs/20 §4)
        services.AddScoped<OrderJobs>();
        services.AddScoped<IRecurringJob, OrderAttentionJob>();
        services.AddScoped<IRecurringJob, PaidBillCompletionJob>();
        return services;
    }
}
