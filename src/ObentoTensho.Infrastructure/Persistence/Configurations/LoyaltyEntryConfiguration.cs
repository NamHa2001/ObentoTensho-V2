using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Loyalty.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class LoyaltyEntryConfiguration : IEntityTypeConfiguration<LoyaltyEntry>
{
    public void Configure(EntityTypeBuilder<LoyaltyEntry> builder)
    {
        var maxType = (byte)Enum.GetValues<LoyaltyTransactionType>().Max();
        builder.ToTable("LoyaltyPoints", table =>
        {
            table.HasCheckConstraint("CK_LoyaltyPoints_Type", $"[TransactionType] BETWEEN 0 AND {maxType}");
            table.HasCheckConstraint("CK_LoyaltyPoints_BalanceAfter", "[BalanceAfter] >= 0");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasColumnName("TransactionType").HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(e => e.ReferenceId).HasColumnType("varchar(100)");
        builder.Property(e => e.Note).HasColumnType("nvarchar(300)");

        // Sổ cái không bao giờ bị xóa theo người dùng (người dùng chỉ được ẩn danh hóa)
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.NoAction);

        // BR-LOY-02: mỗi sự kiện chỉ ghi một lần
        builder.HasIndex(e => new { e.UserId, e.Type, e.ReferenceId }).IsUnique().HasFilter("[ReferenceId] IS NOT NULL");
        builder.HasIndex(e => new { e.UserId, e.Id });
    }
}
