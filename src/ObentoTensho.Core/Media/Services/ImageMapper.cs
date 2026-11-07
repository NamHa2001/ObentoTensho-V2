using ObentoTensho.Contracts.Dtos.Media;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Domain;

namespace ObentoTensho.Core.Media.Services;

public sealed class ImageMapper(IImageFileStore files)
{
    public ImageDto ToDto(StoredImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var variants = image.Status == ImageStatus.Ready
            ? image.Widths.Select(w => new ImageVariantDto(w, files.VariantUrl(image.Id, image.Version, w, image.Format))).ToArray()
            : [];
        return new ImageDto(image.Id, image.Status, image.Width, image.Height, image.AltText, image.BlurHash, variants);
    }
}
