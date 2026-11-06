using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Tables.Domain;

// Bảng Tables (docs/srs/08 §3); đặt tên DiningTable để không trùng khái niệm "bảng" của CSDL
public sealed class DiningTable
{
    public int Id { get; private set; }

    public required int AreaId { get; set; }

    public required string Name { get; set; }

    public required int MinCapacity { get; set; }

    public required int MaxCapacity { get; set; }

    public TableStatus CurrentStatus { get; set; } = TableStatus.Available;

    public int PosX { get; set; }

    public int PosY { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public int Rotation { get; set; }

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; private set; } = [];
}
