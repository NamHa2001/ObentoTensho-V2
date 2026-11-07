namespace ObentoTensho.Core.Media.Abstractions;

public sealed record ProcessedImage(int Width, int Height, string BlurHash, IReadOnlyList<ImageVariant> Variants);
