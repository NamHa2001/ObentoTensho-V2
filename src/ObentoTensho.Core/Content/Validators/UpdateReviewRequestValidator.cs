using FluentValidation;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.Core.Content.Validators;

public sealed class UpdateReviewRequestValidator : AbstractValidator<UpdateReviewRequest>
{
    public UpdateReviewRequestValidator()
    {
        RuleFor(r => r.Rating).InclusiveBetween(1, 5).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.Comment).OptionalText(1000);
    }
}
