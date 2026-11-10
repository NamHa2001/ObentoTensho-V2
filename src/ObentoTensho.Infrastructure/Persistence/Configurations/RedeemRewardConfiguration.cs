using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class RedeemRewardConfiguration : IEntityTypeConfiguration<RedeemReward>
{
    public void Configure(EntityTypeBuilder<RedeemReward> builder)
    {
        builder.ToTable("RedeemRewards", table =>
        {
            // Cùng ràng buộc mức giảm với voucher (BR-VCH-01)
            table.HasCheckConstraint("CK_RedeemRewards_Discount", "[DiscountValue] > 0 AND ([DiscountType] <> 0 OR [DiscountValue] BETWEEN 1 AND 100) AND [DiscountType] BETWEEN 0 AND 1");
            table.HasCheckConstraint("CK_RedeemRewards_Points", "[PointsCost] >= 1");
            table.HasCheckConstraint("CK_RedeemRewards_ValidDays", "[ValidDays] BETWEEN 1 AND 365");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(300);
        builder.Property(r => r.DiscountType).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(r => r.DiscountValue).HasColumnType("decimal(18,2)");
        builder.Property(r => r.MaxDiscountAmount).HasColumnType("decimal(18,0)");
        builder.Property(r => r.MinOrderValue).HasColumnType("decimal(18,0)");
        builder.Property(r => r.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasIndex(r => r.Name).IsUnique();
        builder.HasIndex(r => new { r.IsActive, r.DisplayOrder });
    }
}
