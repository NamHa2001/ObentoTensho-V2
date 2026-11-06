using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Reservations.Domain;

// BR-RSV-11: mỗi lần chuyển trạng thái ghi ai, khi nào, từ → sang, lý do; FromStatus null = lúc tạo
public sealed class ReservationStatusHistory
{
    public long Id { get; private set; }

    public required Guid ReservationId { get; init; }

    public ReservationStatus? FromStatus { get; init; }

    public required ReservationStatus ToStatus { get; init; }

    public required ReservationActor ChangedByType { get; init; }

    public Guid? ChangedById { get; init; }

    public string? Reason { get; init; }

    public required DateTime ChangedAtUtc { get; init; }
}
