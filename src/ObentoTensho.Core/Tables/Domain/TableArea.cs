namespace ObentoTensho.Core.Tables.Domain;

public sealed class TableArea
{
    public int Id { get; private set; }

    public required string Name { get; set; }

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; private set; } = [];
}
