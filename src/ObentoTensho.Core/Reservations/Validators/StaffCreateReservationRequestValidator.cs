using FluentValidation;
using ObentoTensho.Contracts.Dtos.Reservations;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Validators;
using ObentoTensho.Core.Reservations.Abstractions;

namespace ObentoTensho.Core.Reservations.Validators;

public sealed class StaffCreateReservationRequestValidator : AbstractValidator<StaffCreateReservationRequest>
{
    public StaffCreateReservationRequestValidator(IReservationSettings settings)
    {
        RuleFor(r => r.GuestName).GuestName();
        RuleFor(r => r.GuestPhone).GuestPhone();
        RuleFor(r => r.GuestEmail)
            .Must(e => e is null || EmailValidator.IsValid(e.Trim()))
            .WithErrorCode(FieldErrorCodes.InvalidFormat);
        RuleFor(r => r.AdultCount).Adults(settings.MaxGuestsPerBooking);
        RuleFor(r => r.ChildCount).Children(r => r.AdultCount, settings.MaxGuestsPerBooking);
        RuleFor(r => r.SpecialRequest).PlainText();
        RuleFor(r => r.InternalNote).PlainText();
        RuleFor(r => r.Source)
            .Must(s => s is ReservationSource.Phone or ReservationSource.WalkIn)
            .WithErrorCode(FieldErrorCodes.OutOfRange);

        // Chỉ khách đến trực tiếp mới check-in ngay
        RuleFor(r => r.CheckInNow)
            .Must((r, now) => !now || r.Source == ReservationSource.WalkIn)
            .WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.OverrideCapacityReason).PlainText();
    }
}
