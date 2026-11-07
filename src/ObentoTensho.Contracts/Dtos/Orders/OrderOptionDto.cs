namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Tùy chọn đã chọn của một dòng (tên theo ngôn ngữ khách, giá cộng thêm cho 1 phần).</summary>
public sealed record OrderOptionDto(string GroupName, string OptionName, decimal PriceDelta);
