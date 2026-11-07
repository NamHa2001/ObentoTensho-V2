namespace ObentoTensho.Core.Content.Domain;

public sealed class NewsCategory
{
    public int Id { get; private set; }

    public required string Name { get; set; }

    public required string Slug { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; private set; } = [];
}
