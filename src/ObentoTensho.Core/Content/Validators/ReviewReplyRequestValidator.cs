using FluentValidation;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.Core.Content.Validators;

public sealed class ReviewReplyRequestValidator : AbstractValidator<ReviewReplyRequest>
{
    public ReviewReplyRequestValidator() => RuleFor(r => r.Content).RequiredText(1000);
}
