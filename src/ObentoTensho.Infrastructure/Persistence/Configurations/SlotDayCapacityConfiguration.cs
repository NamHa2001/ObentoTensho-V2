using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Reservations.Domain;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class SlotDayCapacityConfiguration : IEntityTypeConfiguration<SlotDayCapacity>
{
    public void Configure(EntityTypeBuilder<SlotDayCapacity> builder)
    {
        builder.ToTable("SlotDayCapacity", table =>
            table.HasCheckConstraint("CK_SlotDayCapacity_Counts", "[BookedCount] >= 0 AND [BookedGuests] >= 0"));

        builder.HasKey(c => new { c.SlotId, c.Date });
        builder.Property(c => c.RowVersion).IsRowVersion();
        builder.HasOne<ServiceSlot>().WithMany().HasForeignKey(c => c.SlotId).OnDelete(DeleteBehavior.Cascade);
    }
}
