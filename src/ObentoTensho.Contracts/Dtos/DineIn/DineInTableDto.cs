using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.DineIn;

/// <summary>Bàn trên sơ đồ gọi món: trạng thái bàn và tóm tắt phiếu đang mở (nếu có).</summary>
public sealed record DineInTableDto(
    int TableId,
    string TableName,
    int AreaId,
    string AreaName,
    int MaxCapacity,
    TableStatus Status,
    Guid? BillId,
    string? BillCode,
    int? GuestCount,
    DateTime? OpenedAtUtc,
    decimal? SubTotal,
    int UnservedCount);
