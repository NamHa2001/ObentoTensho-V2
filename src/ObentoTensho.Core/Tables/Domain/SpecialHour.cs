namespace ObentoTensho.Core.Tables.Domain;

// Giờ của một ngày cụ thể (lễ/Tết), ghi đè OpeningHours; ngày đóng cửa là một dòng IsClosed
public sealed class SpecialHour
{
    public int Id { get; private set; }

    public required DateOnly Date { get; init; }

    public TimeOnly? OpenTime { get; init; }

    public TimeOnly? CloseTime { get; init; }

    public bool IsClosed { get; init; }

    public string? Note { get; init; }

    public required Guid CreatedByStaffId { get; init; }
}
