using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class MenuItemTranslationConfiguration : IEntityTypeConfiguration<MenuItemTranslation>
{
    public void Configure(EntityTypeBuilder<MenuItemTranslation> builder) =>
        MenuTranslationConfiguration.Configure<MenuItemTranslation, MenuItem>(builder, "MenuItemTranslations", "MenuItemId", hasDescriptions: true);
}
