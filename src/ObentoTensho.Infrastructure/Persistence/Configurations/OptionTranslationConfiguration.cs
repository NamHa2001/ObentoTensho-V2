using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class OptionTranslationConfiguration : IEntityTypeConfiguration<OptionTranslation>
{
    public void Configure(EntityTypeBuilder<OptionTranslation> builder) =>
        MenuTranslationConfiguration.Configure<OptionTranslation, MenuOption>(builder, "OptionTranslations", "OptionId", hasDescriptions: false);
}
