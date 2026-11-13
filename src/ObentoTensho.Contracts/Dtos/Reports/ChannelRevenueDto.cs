using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Reports;

/// <summary>Doanh thu theo kênh bán (mang về / tại quán / giao hàng): số đơn đã thu tiền, tiền thu, hoàn tiền, doanh thu.</summary>
public sealed record ChannelRevenueDto(OrderChannel Channel, int Orders, decimal Paid, decimal Refunds, decimal Net);
