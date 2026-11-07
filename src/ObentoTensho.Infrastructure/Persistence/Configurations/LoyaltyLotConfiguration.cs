using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Loyalty.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class LoyaltyLotConfiguration : IEntityTypeConfiguration<LoyaltyLot>
{
    public void Configure(EntityTypeBuilder<LoyaltyLot> builder)
    {
        builder.ToTable("LoyaltyLots", table => table.HasCheckConstraint("CK_LoyaltyLots_RemainingPoints", "[RemainingPoints] >= 0"));

        builder.HasKey(l => l.LedgerId);
        builder.Property(l => l.LedgerId).ValueGeneratedNever();
        builder.HasOne(l => l.Entry).WithOne().HasForeignKey<LoyaltyLot>(l => l.LedgerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.NoAction);

        // FIFO theo hạn; việc định kỳ tìm lô hết hạn/sắp hết hạn còn điểm
        builder.HasIndex(l => new { l.UserId, l.ExpiresAtUtc });
        builder.HasIndex(l => l.ExpiresAtUtc).HasFilter("[RemainingPoints] > 0");
    }
}
