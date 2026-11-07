using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class OptionGroupConfiguration : IEntityTypeConfiguration<OptionGroup>
{
    public void Configure(EntityTypeBuilder<OptionGroup> builder)
    {
        builder.ToTable("OptionGroups", table =>
        {
            // BR-MNU-18: Min ≤ Max; chọn một ⇒ Max = 1; bắt buộc ⇒ Min ≥ 1
            table.HasCheckConstraint(
                "CK_OptionGroups_Selection",
                "[MinSelect] <= [MaxSelect] AND [MaxSelect] >= 1 AND ([SelectionType] = 1 OR [MaxSelect] = 1) AND ([IsRequired] = 0 OR [MinSelect] >= 1)");
            table.HasCheckConstraint("CK_OptionGroups_SelectionType", "[SelectionType] BETWEEN 0 AND 1");
        });

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(g => g.SelectionType).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(g => g.MinSelect).HasColumnType("tinyint");
        builder.Property(g => g.MaxSelect).HasColumnType("tinyint");
        builder.Property(g => g.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(g => g.RowVersion).IsRowVersion();
    }
}
