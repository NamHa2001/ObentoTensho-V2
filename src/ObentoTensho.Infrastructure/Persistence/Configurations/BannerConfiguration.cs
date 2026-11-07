using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Media.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    public void Configure(EntityTypeBuilder<Banner> builder)
    {
        builder.ToTable("Banners", table => table.HasCheckConstraint("CK_Banners_Dates", "[ShowFromDate] IS NULL OR [ShowToDate] IS NULL OR [ShowToDate] >= [ShowFromDate]"));

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Title).HasColumnType("nvarchar(200)").IsRequired();
        builder.Property(b => b.Subtitle).HasColumnType("nvarchar(300)");
        builder.Property(b => b.CtaText).HasColumnType("nvarchar(100)");
        builder.Property(b => b.CtaLink).HasColumnType("nvarchar(300)");
        builder.Property(b => b.ShowFromDate).HasColumnType("date");
        builder.Property(b => b.ShowToDate).HasColumnType("date");
        builder.Property(b => b.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(b => b.RowVersion).IsRowVersion();

        builder.HasOne<StoredImage>().WithMany().HasForeignKey(b => b.ImageId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<StoredImage>().WithMany().HasForeignKey(b => b.ImageMobileId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(b => new { b.IsActive, b.DisplayOrder });
    }
}
