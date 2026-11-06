using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class BlockedDateConfiguration : IEntityTypeConfiguration<BlockedDate>
{
    public void Configure(EntityTypeBuilder<BlockedDate> builder)
    {
        var maxScope = (byte)Enum.GetValues<BlockScope>().Max();

        builder.ToTable("BlockedDates", table =>
        {
            table.HasCheckConstraint("CK_BlockedDates_AppliesTo", $"[AppliesTo] BETWEEN 0 AND {maxScope}");
            table.HasCheckConstraint(
                "CK_BlockedDates_Range",
                "([StartTime] IS NULL AND [EndTime] IS NULL) OR ([StartTime] IS NOT NULL AND [EndTime] IS NOT NULL AND [StartTime] < [EndTime])");
        });

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Date).HasColumnName("BlockedDate");
        builder.Property(b => b.AppliesTo).HasConversion<byte>().HasColumnType("tinyint")
            .HasDefaultValue(BlockScope.Both).ValueGeneratedNever();
        builder.Property(b => b.Reason).HasColumnType("nvarchar(300)");
        builder.HasOne<Staff>().WithMany().HasForeignKey(b => b.CreatedByStaffId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(b => b.Date);
    }
}
