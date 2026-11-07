using FluentValidation;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.Core.Content.Validators;

public sealed class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(r => r.Rating).InclusiveBetween(1, 5).WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.Comment).OptionalText(1000);
    }
}
