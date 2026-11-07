using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Content.Domain;

// docs/srs/08 §7 Gallery
public sealed class GalleryItem
{
    public int Id { get; private set; }

    public GalleryMediaType MediaType { get; set; }

    public Guid? ImageId { get; set; }

    public string? VideoUrl { get; set; }

    public GalleryCategory Category { get; set; }

    public string? Caption { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; private set; } = [];
}
