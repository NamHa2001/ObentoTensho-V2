using FluentValidation;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Errors;

namespace ObentoTensho.Core.Tables.Validators;

// BR-TBL-15: Start < End, MaxReservations ≥ 1; chồng lấn và nằm trong giờ mở kiểm ở Service (cần CSDL)
public sealed class ServiceSlotRequestValidator : AbstractValidator<ServiceSlotRequest>
{
    public const int MaxReservationsLimit = 1000;

    public ServiceSlotRequestValidator()
    {
        RuleFor(r => r.DayOfWeek).Must(d => d is null or >= 0 and <= 6).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.End).Must((r, end) => r.Start < end).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.MaxReservations).InclusiveBetween(1, MaxReservationsLimit).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.MaxGuests).Must(g => g is null or >= 1 and <= MaxReservationsLimit * 20).WithErrorCode(FieldErrorCodes.OutOfRange);
    }
}
