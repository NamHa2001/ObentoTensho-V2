using FluentValidation;
using ObentoTensho.Contracts.Dtos.Reservations;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Auth.Validators;
using ObentoTensho.Core.Reservations.Abstractions;

namespace ObentoTensho.Core.Reservations.Validators;

public sealed class CreateReservationRequestValidator : AbstractValidator<CreateReservationRequest>
{
    public CreateReservationRequestValidator(IReservationSettings settings)
    {
        RuleFor(r => r.GuestName).GuestName();
        RuleFor(r => r.GuestPhone).GuestPhone();
        RuleFor(r => r.GuestEmail).ValidEmail();
        RuleFor(r => r.AdultCount).Adults(settings.MaxGuestsPerBooking);
        RuleFor(r => r.ChildCount).Children(r => r.AdultCount, settings.MaxGuestsPerBooking);
        RuleFor(r => r.SpecialRequest).PlainText();
        RuleFor(r => r.Lang)
            .Must(l => l is null || AuthPolicy.SupportedLanguages.Contains(l, StringComparer.Ordinal))
            .WithErrorCode(FieldErrorCodes.OutOfRange);
    }
}
