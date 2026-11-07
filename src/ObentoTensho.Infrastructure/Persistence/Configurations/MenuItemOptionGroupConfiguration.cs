using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class MenuItemOptionGroupConfiguration : IEntityTypeConfiguration<MenuItemOptionGroup>
{
    public void Configure(EntityTypeBuilder<MenuItemOptionGroup> builder)
    {
        builder.ToTable("MenuItemOptionGroups");
        builder.HasKey(l => new { l.MenuItemId, l.GroupId });
        builder.HasOne<MenuItem>().WithMany().HasForeignKey(l => l.MenuItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<OptionGroup>().WithMany().HasForeignKey(l => l.GroupId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(l => l.GroupId);
    }
}
