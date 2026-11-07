using FluentValidation;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Core.Menu.Validators;

// BR-MNU-18: chọn một ⇒ tối đa 1; bắt buộc ⇒ tối thiểu ≥ 1; Min ≤ Max ≤ 20
public sealed class OptionGroupRequestValidator : AbstractValidator<OptionGroupRequest>
{
    public OptionGroupRequestValidator()
    {
        RuleFor(r => r.Name).RequiredText(100);
        RuleFor(r => r.SelectionType).IsInEnum().WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.MaxSelect)
            .Must((r, max) => max is >= 1 and <= MenuRules.MaxOptionsPerGroup && r.MinSelect >= 0 && r.MinSelect <= max
                && (r.SelectionType != OptionSelectionType.One || max == 1)
                && (!r.IsRequired || r.MinSelect >= 1))
            .WithErrorCode(FieldErrorCodes.OutOfRange);
    }
}
