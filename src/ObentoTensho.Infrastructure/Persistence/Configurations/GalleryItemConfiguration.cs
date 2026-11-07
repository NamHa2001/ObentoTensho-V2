using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Media.Domain;
using ObentoTensho.Infrastructure.Persistence.Conversions;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class GalleryItemConfiguration : IEntityTypeConfiguration<GalleryItem>
{
    public void Configure(EntityTypeBuilder<GalleryItem> builder)
    {
        var types = string.Join(", ", SnakeCaseEnum.AllValues<GalleryMediaType>().Select(v => $"'{v}'"));
        var categories = string.Join(", ", SnakeCaseEnum.AllValues<GalleryCategory>().Select(v => $"'{v}'"));
        builder.ToTable("Gallery", table =>
        {
            table.HasCheckConstraint("CK_Gallery_MediaType", $"[MediaType] IN ({types})");
            table.HasCheckConstraint("CK_Gallery_Category", $"[Category] IN ({categories})");
            table.HasCheckConstraint("CK_Gallery_Media", "([MediaType] = 'image' AND [ImageId] IS NOT NULL) OR ([MediaType] = 'video' AND [VideoUrl] IS NOT NULL)");
        });

        builder.HasKey(g => g.Id);
        builder.Property(g => g.MediaType).HasConversion(new SnakeCaseEnumConverter<GalleryMediaType>()).HasColumnType("varchar(10)");
        builder.Property(g => g.Category).HasConversion(new SnakeCaseEnumConverter<GalleryCategory>()).HasColumnType("varchar(20)");
        builder.Property(g => g.VideoUrl).HasColumnType("nvarchar(500)");
        builder.Property(g => g.Caption).HasColumnType("nvarchar(300)");
        builder.Property(g => g.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(g => g.RowVersion).IsRowVersion();

        builder.HasOne<StoredImage>().WithMany().HasForeignKey(g => g.ImageId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(g => new { g.IsActive, g.Category, g.DisplayOrder });
        builder.HasIndex(g => g.ImageId);
    }
}
