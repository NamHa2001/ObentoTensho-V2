using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Media.Domain;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

// docs/srs/08 §4 MenuItemImages: xóa món ⇒ xóa liên kết ảnh; một ảnh không lặp trong cùng món
internal sealed class MenuItemImageConfiguration : IEntityTypeConfiguration<MenuItemImage>
{
    public void Configure(EntityTypeBuilder<MenuItemImage> builder)
    {
        builder.ToTable("MenuItemImages");
        builder.HasKey(i => i.Id);
        builder.HasOne<MenuItem>().WithMany().HasForeignKey(i => i.MenuItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<StoredImage>().WithMany().HasForeignKey(i => i.ImageId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(i => new { i.MenuItemId, i.ImageId }).IsUnique();
    }
}
