namespace ObentoTensho.Contracts.Dtos.DineIn;

/// <summary>Phiếu bàn đang mở (một bàn một phiếu).</summary>
public sealed record DineInBillDto(
    Guid Id,
    string Code,
    int TableId,
    string TableName,
    int GuestCount,
    DateTime OpenedAtUtc,
    IReadOnlyList<DineInLineDto> Items,
    decimal SubTotal,
    string? Note,
    string Version);
