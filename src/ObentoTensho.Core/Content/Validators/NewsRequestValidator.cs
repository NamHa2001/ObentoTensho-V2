using FluentValidation;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.Core.Content.Validators;

public sealed class NewsRequestValidator : AbstractValidator<NewsRequest>
{
    public const int ContentMaxLength = 200_000;

    public NewsRequestValidator()
    {
        RuleFor(r => r.Title).RequiredText(300);
        RuleFor(r => r.Slug).Slug(300);
        RuleFor(r => r.Summary).OptionalText(500);
        RuleFor(r => r.ContentHtml).RequiredText(ContentMaxLength);
        RuleFor(r => r.AuthorDisplayName).OptionalText(100);
        RuleFor(r => r.MetaTitle).OptionalText(200);
        RuleFor(r => r.MetaDescription).OptionalText(500);
        RuleFor(r => r.Status).IsInEnum().WithErrorCode(FieldErrorCodes.OutOfRange);
    }
}
