namespace ObentoTensho.Contracts.Dtos.Tables;

/// <summary>Khoảng mở cửa <c>[Open, Close)</c> giờ VN, dạng "HH:mm"; <c>CrossesMidnight</c> khi đóng sau 0 giờ hôm sau.</summary>
public sealed record OpeningRangeDto(TimeOnly Open, TimeOnly Close, bool CrossesMidnight);
