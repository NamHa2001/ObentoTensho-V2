namespace ObentoTensho.Contracts.Enums;

// FR-RSV-06: khách đặt trên web, hoặc lễ tân tạo hộ qua điện thoại / khách đến trực tiếp
public enum ReservationSource : byte
{
    Web = 0,
    Phone = 1,
    WalkIn = 2
}
