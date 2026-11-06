namespace ObentoTensho.Contracts.Dtos.Tables;

/// <summary>Giờ mở cửa công khai: lịch tuần và các ngày đặc biệt sắp tới.</summary>
public sealed record PublicOpeningHoursDto(IReadOnlyList<OpeningDayDto> Weekly, IReadOnlyList<SpecialHoursDto> Special);
