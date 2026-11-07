using FluentValidation;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.Core.Content.Validators;

// BR-CNT-30/34, SEC-47
public sealed class BannerRequestValidator : AbstractValidator<BannerRequest>
{
    public BannerRequestValidator(IContentSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RuleFor(r => r.ImageId).NotEmpty().WithErrorCode(FieldErrorCodes.Required);
        RuleFor(r => r.Title).RequiredText(200);
        RuleFor(r => r.Subtitle).OptionalText(300);
        RuleFor(r => r.CtaText).OptionalText(100);
        RuleFor(r => r.CtaLink)
            .Must(l => l is null || (l.Length <= 300 && SafeLinks.IsAllowedCta(l, settings.AllowedCtaHosts)))
            .WithErrorCode(FieldErrorCodes.InvalidFormat);
        RuleFor(r => r.ShowToDate)
            .Must((r, to) => to is null || r.ShowFromDate is null || to >= r.ShowFromDate)
            .WithErrorCode(FieldErrorCodes.OutOfRange);
    }
}
