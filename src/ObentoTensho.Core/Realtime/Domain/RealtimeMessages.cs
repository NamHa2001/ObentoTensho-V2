using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Realtime;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Reservations.Domain;

namespace ObentoTensho.Core.Realtime.Domain;

// RT-01..04: ai nhận sự kiện nào
public static class RealtimeMessages
{
    public static RealtimeMessage ReservationCreated(Reservation reservation) =>
        new(RealtimeEventNames.ReservationCreated, RealtimeGroups.StaffWith(StaffCapability.Reservations), Payload(reservation));

    public static RealtimeMessage ReservationChanged(Reservation reservation) =>
        new(RealtimeEventNames.ReservationStatusChanged, RealtimeGroups.StaffWith(StaffCapability.Reservations), Payload(reservation));

    public static RealtimeMessage Availability(Reservation reservation) =>
        new(RealtimeEventNames.SlotAvailabilityChanged, [RealtimeGroups.Availability(reservation.ReservedDate)], new AvailabilityChange(reservation.ReservedDate, reservation.SlotId));

    public static RealtimeMessage OrderCreated(Order order) =>
        new(RealtimeEventNames.OrderCreated, RealtimeGroups.StaffWith(StaffCapability.OrdersView), Payload(order));

    // Nhân viên và khách đang xem đơn đó
    public static RealtimeMessage OrderChanged(Order order) =>
        new(RealtimeEventNames.OrderStatusChanged, [.. RealtimeGroups.StaffWith(StaffCapability.OrdersView), RealtimeGroups.Order(order.Id)], Payload(order));

    public static RealtimeMessage ContactCreated(ContactMessage message) =>
        new(RealtimeEventNames.ContactCreated, RealtimeGroups.StaffWith(StaffCapability.Feedback), new ContactEventDto(message.Id));

    private static ReservationEventDto Payload(Reservation reservation) =>
        new(reservation.Id, reservation.ReservationCode, reservation.Status, reservation.SlotId);

    private static OrderEventDto Payload(Order order) =>
        new(order.Id, order.OrderCode, order.Status, order.PaymentStatus);
}
