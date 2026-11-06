namespace ObentoTensho.Core.Tables.Domain;

// Khoảng [Start, End) theo giờ địa phương, có thể kéo sang ngày hôm sau (qua nửa đêm)
public readonly record struct OpenInterval(DateTime Start, DateTime End)
{
    public bool Contains(DateTime start, DateTime end) => start >= Start && end <= End;

    public bool Overlaps(OpenInterval other) => Start < other.End && other.Start < End;
}
