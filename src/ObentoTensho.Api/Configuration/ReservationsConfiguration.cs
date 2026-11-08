using FluentValidation;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Reservations;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Reservations.Services;
using ObentoTensho.Core.Reservations.Validators;
using ObentoTensho.Core.Verification.Services;

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
        services.AddScoped<OtpService>();
        services.AddSingleton<TurnstileOutage>();
        services.AddScoped<TurnstileGate>();
        services.AddScoped<TurnstileFilter>();
        services.AddScoped<GuestTurnstileFilter>();

        // Việc định kỳ (docs/srs/20 §4)
        services.AddScoped<ReservationJobs>();
        services.AddScoped<IRecurringJob, ReservationReminderJob>();
        services.AddScoped<IRecurringJob, ReservationNoShowJob>();
        services.AddScoped<IRecurringJob, ReservationPendingCutoffJob>();
        services.AddScoped<IRecurringJob, ReservationPendingAlertJob>();
        services.AddScoped<IRecurringJob, ReservationTableDayStartJob>();
        return services;
    }
}
