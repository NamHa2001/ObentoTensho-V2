using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Localization.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class BannerTranslationConfiguration : IEntityTypeConfiguration<BannerTranslation>
{
    public void Configure(EntityTypeBuilder<BannerTranslation> builder) =>
        ContentTranslationConfiguration.Configure<BannerTranslation, Banner>(builder, "BannerTranslations", "BannerId", 200, 300, ContentTranslationConfiguration.Columns.Summary | ContentTranslationConfiguration.Columns.Cta);
}
