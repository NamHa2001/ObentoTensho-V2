using ObentoTensho.Contracts.Dtos.Media;

namespace ObentoTensho.Contracts.Dtos.Menu;

public sealed record MenuItemImagesDto(ImageDto? MainImage, IReadOnlyList<ImageDto> Images);
