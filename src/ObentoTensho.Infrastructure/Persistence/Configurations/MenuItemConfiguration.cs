using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        var maxStatus = (byte)Enum.GetValues<SaleStatus>().Max();

        builder.ToTable("MenuItems", table =>
        {
            // BR-MNU-05
            table.HasCheckConstraint("CK_MenuItems_Price", "[Price] > 0 AND ([OriginalPrice] IS NULL OR [OriginalPrice] > [Price])");
            table.HasCheckConstraint("CK_MenuItems_Status", $"[Status] BETWEEN 0 AND {maxStatus}");
        });

        builder.HasKey(i => i.Id);
        builder.Property(i => i.NameVi).HasColumnType("nvarchar(150)").IsRequired();
        builder.Property(i => i.Slug).HasColumnType("varchar(200)").IsRequired();
        builder.Property(i => i.NameSearch).HasColumnType("nvarchar(300)").IsRequired();
        builder.Property(i => i.ShortDesc).HasColumnType("nvarchar(200)");
        builder.Property(i => i.FullDescHtml).HasColumnType("nvarchar(max)");
        builder.Property(i => i.Price).HasColumnType("decimal(18,0)");
        builder.Property(i => i.OriginalPrice).HasColumnType("decimal(18,0)");
        builder.Property(i => i.AllowTakeaway).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(i => i.Status).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(i => i.Allergens).HasColumnType("varchar(100)");
        builder.Property(i => i.RowVersion).IsRowVersion();

        builder.HasOne<MenuCategory>().WithMany().HasForeignKey(i => i.CategoryId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(i => i.Slug).IsUnique();
        builder.HasIndex(i => new { i.CategoryId, i.Status, i.DisplayOrder });
        builder.HasIndex(i => i.NameSearch);
    }
}
