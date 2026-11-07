using ObentoTensho.Contracts.Dtos.Promotions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Promotions.Domain;

namespace ObentoTensho.Core.Promotions.Services;

public static class PromotionMappings
{
    public static AdminPromotionDto ToAdmin(this Promotion p) =>
        new(p.Id, p.Title, p.Slug, p.DescriptionHtml, p.StartDate, p.EndDate, p.PublicTeaser, p.IsActive, p.CreatedAtUtc, RowVersions.Encode(p.RowVersion));
}
