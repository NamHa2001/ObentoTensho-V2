using FluentValidation;
using ObentoTensho.Contracts.Dtos.Reservations;
using ObentoTensho.Core.Reservations.Services;
using ObentoTensho.Core.Reservations.Validators;

namespace ObentoTensho.Api.Configuration;

// Đặt bàn (docs/srs/11)
public static class ReservationsConfiguration
{
    public static IServiceCollection AddReservationServices(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<CreateReservationRequest>, CreateReservationRequestValidator>();
        services.AddSingleton<IValidator<StaffCreateReservationRequest>, StaffCreateReservationRequestValidator>();

        services.AddScoped<ReservationSchedule>();
        services.AddScoped<ReservationBooking>();
        services.AddScoped<ReservationTransitions>();
        services.AddScoped<PublicReservationService>();
        services.AddScoped<ReservationDeskService>();
        services.AddScoped<ReservationStaffService>();
        return services;
    }
}
