using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Content.Services;

// BR-CNT-41: đổi slug ⇒ giữ 301 từ slug cũ; đổi về một slug cũ thì bỏ chuyển hướng đó. Gọi trong transaction của bên sửa
public sealed class SlugHistory(ISlugRedirectRepository redirects, IClock clock)
{
    public async Task RecordAsync(string entityType, int entityId, string oldSlug, string newSlug, CancellationToken cancellationToken)
    {
        if (string.Equals(oldSlug, newSlug, StringComparison.Ordinal))
        {
            return;
        }

        await redirects.RemoveAsync(entityType, newSlug, cancellationToken);
        await redirects.RemoveAsync(entityType, oldSlug, cancellationToken);
        redirects.Add(new SlugRedirect { EntityType = entityType, OldSlug = oldSlug, EntityId = entityId, CreatedAtUtc = clock.UtcNow });
    }

    public async Task<int?> TargetAsync(string entityType, string oldSlug, CancellationToken cancellationToken) =>
        (await redirects.FindAsync(entityType, oldSlug, cancellationToken))?.EntityId;
}
