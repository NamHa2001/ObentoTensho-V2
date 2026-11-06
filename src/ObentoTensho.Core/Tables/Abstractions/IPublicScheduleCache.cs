using ObentoTensho.Contracts.Dtos.Tables;

namespace ObentoTensho.Core.Tables.Abstractions;

// PERF-11, BR-TBL-10: giờ mở cửa công khai được cache; mọi thay đổi lịch phải xóa cache ngay
public interface IPublicScheduleCache
{
    Task<PublicOpeningHoursDto> GetOrCreateAsync(Func<CancellationToken, Task<PublicOpeningHoursDto>> factory, CancellationToken cancellationToken);

    void Invalidate();
}
