using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class OptionGroupTranslationConfiguration : IEntityTypeConfiguration<OptionGroupTranslation>
{
    public void Configure(EntityTypeBuilder<OptionGroupTranslation> builder) =>
        MenuTranslationConfiguration.Configure<OptionGroupTranslation, OptionGroup>(builder, "OptionGroupTranslations", "GroupId", hasDescriptions: false);
}
