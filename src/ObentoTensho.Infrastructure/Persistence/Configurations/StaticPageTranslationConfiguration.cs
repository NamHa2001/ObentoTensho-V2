using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Localization.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class StaticPageTranslationConfiguration : IEntityTypeConfiguration<StaticPageTranslation>
{
    public void Configure(EntityTypeBuilder<StaticPageTranslation> builder) =>
        ContentTranslationConfiguration.Configure<StaticPageTranslation, StaticPage>(builder, "StaticPageTranslations", "StaticPageId", 200, 0, ContentTranslationConfiguration.Columns.Body);
}
