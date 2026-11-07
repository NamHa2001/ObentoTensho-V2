using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Menu.Domain;

// BR-MNU-18: nhóm tùy chọn dùng chung cho nhiều món
public sealed class OptionGroup
{
    public int Id { get; private set; }

    public required string Name { get; set; }

    public OptionSelectionType SelectionType { get; set; }

    public bool IsRequired { get; set; }

    public byte MinSelect { get; set; }

    public byte MaxSelect { get; set; } = 1;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; private set; } = [];
}
