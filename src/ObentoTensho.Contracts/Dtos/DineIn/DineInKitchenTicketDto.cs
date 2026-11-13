namespace ObentoTensho.Contracts.Dtos.DineIn;

/// <summary>Phiếu bếp của một bàn: chỉ món chưa ra, chưa hủy (SEC-12: không có tiền, liên hệ).</summary>
public sealed record DineInKitchenTicketDto(Guid BillId, string Code, string TableName, IReadOnlyList<DineInKitchenLineDto> Items);
