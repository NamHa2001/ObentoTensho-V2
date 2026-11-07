using FluentValidation;
using ObentoTensho.Contracts.Dtos.Promotions;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.Core.Promotions.Validators;

// BR-VCH-13/17: ngày kết thúc không trước ngày bắt đầu (cho phép chương trình một ngày — CHECK EndDate >= StartDate của docs/srs/08)
public sealed class PromotionRequestValidator : AbstractValidator<PromotionRequest>
{
    public const int DescriptionMaxLength = 20_000;

    public PromotionRequestValidator()
    {
        RuleFor(r => r.Title).RequiredText(200);
        RuleFor(r => r.Slug).Slug(200);
        RuleFor(r => r.DescriptionHtml).OptionalText(DescriptionMaxLength);
        RuleFor(r => r.EndDate).Must((r, end) => end >= r.StartDate).WithErrorCode(FieldErrorCodes.OutOfRange);
    }
}
