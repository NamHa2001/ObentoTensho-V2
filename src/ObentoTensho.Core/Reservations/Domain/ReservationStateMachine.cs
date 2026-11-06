using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Reservations.Domain;

// BR-RSV-05, bảng trạng thái docs/srs/11 §6: chỉ các bước dưới đây hợp lệ; Completed và Cancelled là trạng thái cuối
public static class ReservationStateMachine
{
    public static bool CanTransition(ReservationStatus from, ReservationStatus to, ReservationActor actor) => (from, to) switch
    {
        (ReservationStatus.Pending, ReservationStatus.Confirmed) => actor == ReservationActor.Staff,
        (ReservationStatus.Pending, ReservationStatus.Cancelled) => true,
        (ReservationStatus.Confirmed, ReservationStatus.CheckedIn) => actor == ReservationActor.Staff,
        (ReservationStatus.Confirmed, ReservationStatus.Cancelled) => actor != ReservationActor.System,
        (ReservationStatus.Confirmed, ReservationStatus.NoShow) => actor != ReservationActor.Guest,
        (ReservationStatus.CheckedIn, ReservationStatus.Completed) => actor == ReservationActor.Staff,

        // Khách đến muộn sau khi bị tự đánh dấu không đến — chỉ nhân viên, cùng ngày, có lý do (kiểm ở Service)
        (ReservationStatus.NoShow, ReservationStatus.CheckedIn) => actor == ReservationActor.Staff,
        _ => false,
    };

    // Nhân viên từ chối/hủy hoặc khôi phục no-show phải ghi lý do
    public static bool RequiresReason(ReservationStatus from, ReservationStatus to, ReservationActor actor) =>
        actor == ReservationActor.Staff && (to == ReservationStatus.Cancelled || from == ReservationStatus.NoShow);

    // Hủy và không đến trả chỗ lại cho ca (BR-RSV-04)
    public static bool ReleasesCapacity(ReservationStatus to) => to is ReservationStatus.Cancelled or ReservationStatus.NoShow;
}
