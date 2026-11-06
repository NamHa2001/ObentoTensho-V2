using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Reservations.Domain;

// docs/srs/08 §4. Ngày/giờ đặt là giờ VN; ReservedAtUtc tính từ hai cột đó để so sánh và chạy job.
public sealed class Reservation
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string ReservationCode { get; init; }

    public required int DailySeq { get; init; }

    public string? AccessTokenHash { get; init; }

    public Guid? UserId { get; init; }

    public required string GuestName { get; init; }

    public required string GuestPhone { get; init; }

    public string? GuestEmail { get; init; }

    public bool OtpRequired { get; init; }

    public DateTime? ContactVerifiedAtUtc { get; set; }

    public DateTime? ContactConfirmedAtUtc { get; set; }

    public Guid? ConfirmedByStaffId { get; set; }

    public string? ConfirmNote { get; set; }

    public byte CallAttempts { get; set; }

    public ReservationSource Source { get; init; } = ReservationSource.Web;

    public Guid? CreatedByStaffId { get; init; }

    public required string Lang { get; init; }

    public required DateOnly ReservedDate { get; init; }

    public required TimeOnly ReservedTime { get; init; }

    public required DateTime ReservedAtUtc { get; init; }

    public required int SlotId { get; init; }

    public required int AdultCount { get; init; }

    public int ChildCount { get; init; }

    public int? TableId { get; set; }

    public string? SpecialRequest { get; init; }

    public string? InternalNote { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    public string? CancelReason { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public ReservationActor? CancelledByType { get; set; }

    public Guid? CancelledById { get; set; }

    public DateTime? ReminderSentAtUtc { get; set; }

    public int PointsEarned { get; set; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; private set; } = [];

    public int GuestCount => AdultCount + ChildCount;

    // Trạng thái còn giữ chỗ trong ca (BR-RSV-04)
    public bool HoldsCapacity => Status is ReservationStatus.Pending or ReservationStatus.Confirmed or ReservationStatus.CheckedIn or ReservationStatus.Completed;
}
