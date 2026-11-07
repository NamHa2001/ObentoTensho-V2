using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Promotions.Domain;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        var maxSource = (byte)Enum.GetValues<VoucherSource>().Max();
        var maxApproval = (byte)Enum.GetValues<ApprovalStatus>().Max();
        builder.ToTable("Vouchers", table =>
        {
            // BR-VCH-01
            table.HasCheckConstraint("CK_Vouchers_Discount", "[DiscountValue] > 0 AND ([DiscountType] <> 0 OR [DiscountValue] BETWEEN 1 AND 100) AND [DiscountType] BETWEEN 0 AND 1");
            table.HasCheckConstraint("CK_Vouchers_Usage", "[UsedCount] >= 0 AND [MaxUsagePerUser] >= 1 AND ([MaxUsageTotal] IS NULL OR [MaxUsageTotal] >= 1)");
            table.HasCheckConstraint("CK_Vouchers_Window", "[StartUtc] IS NULL OR [EndUtc] IS NULL OR [EndUtc] > [StartUtc]");
            table.HasCheckConstraint("CK_Vouchers_Source", $"[Source] BETWEEN 0 AND {maxSource}");
            table.HasCheckConstraint("CK_Vouchers_ApprovalStatus", $"[ApprovalStatus] BETWEEN 0 AND {maxApproval}");
        });

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Code).HasColumnType("varchar(50)").IsRequired();
        builder.Property(v => v.DiscountType).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(v => v.DiscountValue).HasColumnType("decimal(18,2)");
        builder.Property(v => v.MaxDiscountAmount).HasColumnType("decimal(18,0)");
        builder.Property(v => v.MinOrderValue).HasColumnType("decimal(18,0)");
        builder.Property(v => v.MaxUsagePerUser).HasDefaultValue(1).ValueGeneratedNever();
        builder.Property(v => v.ApplicableTier).HasConversion<byte?>().HasColumnType("tinyint");
        builder.Property(v => v.Source).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(v => v.ApprovalStatus).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(v => v.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(v => v.RowVersion).IsRowVersion();

        builder.HasOne<Promotion>().WithMany().HasForeignKey(v => v.PromotionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<User>().WithMany().HasForeignKey(v => v.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Staff>().WithMany().HasForeignKey(v => v.CreatedByStaffId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Staff>().WithMany().HasForeignKey(v => v.ReviewedByStaffId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(v => v.Code).IsUnique();
        builder.HasIndex(v => v.OwnerUserId);
        builder.HasIndex(v => v.PromotionId);

        // BR-LOY-20: mỗi khách mỗi năm một voucher sinh nhật
        builder.HasIndex(v => new { v.OwnerUserId, v.BirthdayYear }).IsUnique().HasFilter("[BirthdayYear] IS NOT NULL");
        builder.HasIndex(v => v.BatchId);
        builder.HasIndex(v => new { v.ApprovalStatus, v.CreatedAtUtc });
    }
}
