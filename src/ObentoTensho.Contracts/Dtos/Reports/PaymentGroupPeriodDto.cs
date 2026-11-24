using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Reports;

/// <summary>Doanh thu ròng và thực nhận của một nhóm trong một kỳ (ngày, tuần, tháng).</summary>
public sealed record PaymentGroupPeriodDto(string Period, PaymentGroup Group, decimal NetRevenue, decimal ReceivedAmount);
