using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Tables.Domain;

// BR-TBL-04, BR-TBL-18: khóa cả ngày hoặc khung [Start, End), riêng đặt bàn / đặt món / cả hai
public sealed class BlockedDate
{
    public int Id { get; private set; }

    public required DateOnly Date { get; init; }

    public BlockScope AppliesTo { get; init; } = BlockScope.Both;

    public TimeOnly? StartTime { get; init; }

    public TimeOnly? EndTime { get; init; }

    public string? Reason { get; init; }

    public required Guid CreatedByStaffId { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public bool Covers(DateOnly date, TimeOnly localTime, BlockScope scope)
    {
        if (date != Date || (AppliesTo != BlockScope.Both && AppliesTo != scope))
        {
            return false;
        }

        return StartTime is not { } start || EndTime is not { } end || (localTime >= start && localTime < end);
    }
}
