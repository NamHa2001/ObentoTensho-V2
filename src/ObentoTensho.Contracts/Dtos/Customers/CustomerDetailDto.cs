using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Customers;

public sealed record CustomerDetailDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    MemberTier MemberTier,
    int CurrentPoints,
    int PointsDebt,
    int AccumulatedPoints,
    bool IsActive,
    bool IsEmailVerified,
    DateTime CreatedAtUtc,
    IReadOnlyList<CustomerReservationDto> RecentReservations,
    IReadOnlyList<CustomerOrderDto> RecentOrders,
    IReadOnlyList<CustomerVoucherDto> Vouchers);
