using ObentoTensho.Core.Common;
using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Core.Settings.Domain;

namespace ObentoTensho.Core.Settings.Services;

// BR-RPT-20: mọi khóa trong danh mục đều có dòng trong CSDL; giá trị đã sửa không bị ghi đè
public sealed class SettingsSeeder(ISettingsRepository settings, ISettingsSnapshot snapshot, IUnitOfWork unitOfWork)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var rows = (await settings.ListForUpdateAsync(cancellationToken)).ToDictionary(s => s.Key, StringComparer.Ordinal);
        foreach (var definition in SettingsCatalog.All)
        {
            if (!rows.TryGetValue(definition.Key, out var row))
            {
                row = new SystemSetting { Key = definition.Key, Value = definition.Default, ValueType = definition.Type, Description = definition.Description };
                settings.Add(row);
            }

            row.ValueType = definition.Type;
            row.MinValue = definition.Min;
            row.MaxValue = definition.Max;
            row.Description = definition.Description;
            row.RequiresStepUp = definition.Protected;
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // Instance khác vừa seed
        }

        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken) =>
        snapshot.Replace((await settings.ListAsync(cancellationToken)).ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal));
}
