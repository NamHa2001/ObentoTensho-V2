using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class MenuCategoryTranslationConfiguration : IEntityTypeConfiguration<MenuCategoryTranslation>
{
    public void Configure(EntityTypeBuilder<MenuCategoryTranslation> builder) =>
        MenuTranslationConfiguration.Configure<MenuCategoryTranslation, MenuCategory>(builder, "MenuCategoryTranslations", "CategoryId", hasDescriptions: false);
}
