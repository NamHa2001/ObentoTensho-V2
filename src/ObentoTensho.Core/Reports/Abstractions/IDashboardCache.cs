namespace ObentoTensho.Core.Reports.Abstractions;

// BR-RPT-04: KPI đệm 30–60 giây, theo từng loại bảng tin
public interface IDashboardCache
{
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken);
}
