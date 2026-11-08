using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Localization.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class GalleryTranslationConfiguration : IEntityTypeConfiguration<GalleryTranslation>
{
    public void Configure(EntityTypeBuilder<GalleryTranslation> builder) =>
        ContentTranslationConfiguration.Configure<GalleryTranslation, GalleryItem>(builder, "GalleryTranslations", "GalleryItemId", 300, 0, ContentTranslationConfiguration.Columns.None, titleColumn: "Caption");
}
