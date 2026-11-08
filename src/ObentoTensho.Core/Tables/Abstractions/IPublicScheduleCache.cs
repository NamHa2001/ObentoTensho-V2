using ObentoTensho.Contracts.Dtos.Tables;

namespace ObentoTensho.Core.Tables.Abstractions;

// PERF-11, BR-TBL-10: giờ mở cửa công khai và các dòng lịch (giờ mở cửa, giờ đặc biệt, ngày khóa, ca) được cache;
// mọi thay đổi lịch phải gọi Invalidate sau khi commit
public interface IPublicScheduleCache
{
    Task<PublicOpeningHoursDto> GetOrCreateAsync(Func<CancellationToken, Task<PublicOpeningHoursDto>> factory, CancellationToken cancellationToken);

    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken)
        where T : class;

    void Invalidate();
}
