using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Localization.Domain;

namespace ObentoTensho.Core.Localization.Abstractions;

// PERF-10/59: bản dịch cache theo (loại, ngôn ngữ); lưu bản dịch nào thì xóa hết
public interface ITranslationCache
{
    Task<IReadOnlyList<ContentTranslation>> GetOrCreateAsync(
        TranslatableContent kind,
        string lang,
        Func<CancellationToken, Task<IReadOnlyList<ContentTranslation>>> factory,
        CancellationToken cancellationToken);

    void Invalidate();
}
