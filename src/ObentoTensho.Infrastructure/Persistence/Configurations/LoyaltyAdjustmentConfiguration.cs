using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Loyalty.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class LoyaltyAdjustmentConfiguration : IEntityTypeConfiguration<LoyaltyAdjustment>
{
    public void Configure(EntityTypeBuilder<LoyaltyAdjustment> builder)
    {
        var maxStatus = (byte)Enum.GetValues<AdjustmentStatus>().Max();
        builder.ToTable("LoyaltyAdjustments", table =>
        {
            table.HasCheckConstraint("CK_LoyaltyAdjustments_Points", "[Points] <> 0");
            table.HasCheckConstraint("CK_LoyaltyAdjustments_Status", $"[Status] BETWEEN 0 AND {maxStatus}");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Reason).HasColumnType("nvarchar(300)").IsRequired();
        builder.Property(a => a.ReviewNote).HasColumnType("nvarchar(300)");
        builder.Property(a => a.Status).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(a => a.RowVersion).IsRowVersion();
        builder.Ignore(a => a.ReferenceId);

        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Staff>().WithMany().HasForeignKey(a => a.RequestedByStaffId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Staff>().WithMany().HasForeignKey(a => a.ReviewedByStaffId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(a => new { a.Status, a.RequestedAtUtc });
        builder.HasIndex(a => a.UserId);
    }
}
