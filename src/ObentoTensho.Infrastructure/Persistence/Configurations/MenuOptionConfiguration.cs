using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class MenuOptionConfiguration : IEntityTypeConfiguration<MenuOption>
{
    public void Configure(EntityTypeBuilder<MenuOption> builder)
    {
        var maxStatus = (byte)Enum.GetValues<SaleStatus>().Max();

        builder.ToTable("Options", table =>
        {
            table.HasCheckConstraint("CK_Options_PriceDelta", $"[PriceDelta] >= 0 AND [PriceDelta] <= {MenuRules.MaxPriceDelta:0}");
            table.HasCheckConstraint("CK_Options_Status", $"[Status] BETWEEN 0 AND {maxStatus}");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Name).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(o => o.PriceDelta).HasColumnType("decimal(18,0)");
        builder.Property(o => o.Status).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(o => o.Allergens).HasColumnType("varchar(100)");
        builder.Property(o => o.RowVersion).IsRowVersion();

        builder.HasOne<OptionGroup>().WithMany().HasForeignKey(o => o.GroupId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(o => o.GroupId);
    }
}
