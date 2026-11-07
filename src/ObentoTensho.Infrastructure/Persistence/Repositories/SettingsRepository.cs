using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Core.Settings.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class SettingsRepository(AppDbContext db) : ISettingsRepository
{
    public async Task<IReadOnlyList<SystemSetting>> ListAsync(CancellationToken cancellationToken) =>
        await db.SystemSettings.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SystemSetting>> ListForUpdateAsync(CancellationToken cancellationToken) =>
        await db.SystemSettings.ToListAsync(cancellationToken);

    public void Add(SystemSetting setting) => db.SystemSettings.Add(setting);
}
