using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Localization.Domain;
using ObentoTensho.Core.Promotions.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class PromotionTranslationConfiguration : IEntityTypeConfiguration<PromotionTranslation>
{
    public void Configure(EntityTypeBuilder<PromotionTranslation> builder) =>
        ContentTranslationConfiguration.Configure<PromotionTranslation, Promotion>(builder, "PromotionTranslations", "PromotionId", 200, 0, ContentTranslationConfiguration.Columns.Body);
}
