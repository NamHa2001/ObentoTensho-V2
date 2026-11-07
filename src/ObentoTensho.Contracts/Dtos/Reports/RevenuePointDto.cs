namespace ObentoTensho.Contracts.Dtos.Reports;

public sealed record RevenuePointDto(string Period, decimal Paid, decimal Refunds, decimal Net);
