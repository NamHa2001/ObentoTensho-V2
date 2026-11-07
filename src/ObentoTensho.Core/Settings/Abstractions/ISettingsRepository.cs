using ObentoTensho.Core.Settings.Domain;

namespace ObentoTensho.Core.Settings.Abstractions;

public interface ISettingsRepository
{
    Task<IReadOnlyList<SystemSetting>> ListAsync(CancellationToken cancellationToken);

    // Bản được theo dõi
    Task<IReadOnlyList<SystemSetting>> ListForUpdateAsync(CancellationToken cancellationToken);

    void Add(SystemSetting setting);
}
