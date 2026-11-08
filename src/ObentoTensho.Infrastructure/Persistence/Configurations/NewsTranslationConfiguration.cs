using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Localization.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class NewsTranslationConfiguration : IEntityTypeConfiguration<NewsTranslation>
{
    public void Configure(EntityTypeBuilder<NewsTranslation> builder) =>
        ContentTranslationConfiguration.Configure<NewsTranslation, NewsArticle>(builder, "NewsTranslations", "NewsId", 300, 500, ContentTranslationConfiguration.Columns.Summary | ContentTranslationConfiguration.Columns.Body | ContentTranslationConfiguration.Columns.Meta);
}
