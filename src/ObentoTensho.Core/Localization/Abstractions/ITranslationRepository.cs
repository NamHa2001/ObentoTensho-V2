using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Localization.Domain;

namespace ObentoTensho.Core.Localization.Abstractions;

public interface ITranslationRepository
{
    Task<ContentTranslation?> FindAsync(TranslatableContent kind, int entityId, string lang, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContentTranslation>> ListForEntityAsync(TranslatableContent kind, int entityId, CancellationToken cancellationToken);

    // Mọi bản dịch (kể cả nháp/lỗi thời) của một loại ở một ngôn ngữ — nội dung một quán đủ nhỏ để giữ trong bộ nhớ
    Task<IReadOnlyList<ContentTranslation>> ListAsync(TranslatableContent kind, string lang, CancellationToken cancellationToken);

    void Add(ContentTranslation translation);
}
