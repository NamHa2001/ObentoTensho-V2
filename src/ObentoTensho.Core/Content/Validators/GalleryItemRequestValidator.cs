using FluentValidation;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Menu.Validators;

namespace ObentoTensho.Core.Content.Validators;

// BR-CNT-30/35: ảnh qua pipeline, video chỉ YouTube/TikTok
public sealed class GalleryItemRequestValidator : AbstractValidator<GalleryItemRequest>
{
    public GalleryItemRequestValidator()
    {
        RuleFor(r => r.MediaType).IsInEnum().WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.Category).IsInEnum().WithErrorCode(FieldErrorCodes.OutOfRange);
        RuleFor(r => r.ImageId)
            .Must((r, id) => r.MediaType == GalleryMediaType.Image ? id is not null && id != Guid.Empty : id is null)
            .WithErrorCode(FieldErrorCodes.Required);
        RuleFor(r => r.VideoUrl)
            .Must((r, url) => r.MediaType == GalleryMediaType.Video ? url is { Length: <= 500 } && SafeLinks.VideoEmbedUrl(url) is not null : url is null)
            .WithErrorCode(FieldErrorCodes.InvalidFormat);
        RuleFor(r => r.Caption).OptionalText(300);
    }
}
