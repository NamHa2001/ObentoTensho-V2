using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Reservations.Abstractions;

// docs/srs/09 §5: lọc theo ngày VN, trạng thái, tìm theo tên/mã/SĐT; pageSize ≤ 100
public sealed record ReservationQuery(
    DateOnly? FromDate,
    DateOnly? ToDate,
    ReservationStatus? Status,
    string? Search,
    int Page,
    int PageSize);
