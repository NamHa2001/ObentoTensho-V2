namespace ObentoTensho.Core.Tables.Domain;

// Một khoảng mở cửa của một ngày trong tuần; ngày nghỉ là một dòng IsClosed (BR-TBL-12)
public sealed class OpeningHour
{
    public int Id { get; private set; }

    public required byte DayOfWeek { get; init; }

    public required TimeOnly OpenTime { get; init; }

    public required TimeOnly CloseTime { get; init; }

    public bool CrossesMidnight { get; init; }

    public bool IsClosed { get; init; }

    public byte[] RowVersion { get; private set; } = [];
}
