using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Media.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class StoredImageConfiguration : IEntityTypeConfiguration<StoredImage>
{
    public void Configure(EntityTypeBuilder<StoredImage> builder)
    {
        var maxStatus = (byte)Enum.GetValues<ImageStatus>().Max();
        builder.ToTable("Images", table =>
        {
            table.HasCheckConstraint("CK_Images_Status", $"[Status] BETWEEN 0 AND {maxStatus}");
            table.HasCheckConstraint(
                "CK_Images_Uploader",
                "([CreatedByStaffId] IS NOT NULL AND [CreatedByUserId] IS NULL) OR ([CreatedByStaffId] IS NULL AND [CreatedByUserId] IS NOT NULL)");
        });

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.OriginalPath).HasColumnType("nvarchar(300)").IsRequired();
        builder.Property(i => i.BlurHash).HasColumnType("varchar(40)");
        builder.Property(i => i.Status).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(i => i.VariantWidths).HasColumnType("varchar(60)");
        builder.Property(i => i.Format).HasColumnType("varchar(10)").IsRequired();
        builder.Property(i => i.Version).HasDefaultValue(1).ValueGeneratedNever();
        builder.Property(i => i.AltText).HasColumnType("nvarchar(200)").IsRequired();
        builder.Property(i => i.ContentHash).HasColumnType("char(64)").IsRequired();
        builder.Ignore(i => i.Widths);

        builder.HasOne<Staff>().WithMany().HasForeignKey(i => i.CreatedByStaffId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(i => i.ContentHash);
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.CreatedAtUtc).HasDatabaseName("IX_Images_Regenerate").HasFilter("[RegenerateRequested] = 1");
    }
}
