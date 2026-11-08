using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Media.Domain;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class MenuCategoryConfiguration : IEntityTypeConfiguration<MenuCategory>
{
    public void Configure(EntityTypeBuilder<MenuCategory> builder)
    {
        builder.ToTable("MenuCategories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(c => c.Slug).HasColumnType("varchar(150)").IsRequired();
        builder.Property(c => c.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(c => c.RowVersion).IsRowVersion();
        builder.HasOne<StoredImage>().WithMany().HasForeignKey(c => c.ImageId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(c => c.Slug).IsUnique();
    }
}
