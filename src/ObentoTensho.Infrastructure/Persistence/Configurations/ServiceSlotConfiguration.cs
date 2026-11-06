using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class ServiceSlotConfiguration : IEntityTypeConfiguration<ServiceSlot>
{
    public void Configure(EntityTypeBuilder<ServiceSlot> builder)
    {
        builder.ToTable("ServiceSlots", table =>
        {
            table.HasCheckConstraint("CK_ServiceSlots_DayOfWeek", "[DayOfWeek] IS NULL OR [DayOfWeek] BETWEEN 0 AND 6");
            table.HasCheckConstraint("CK_ServiceSlots_Range", "[StartTime] < [EndTime]");
            table.HasCheckConstraint("CK_ServiceSlots_Capacity", "[MaxReservations] >= 1 AND ([MaxGuests] IS NULL OR [MaxGuests] >= 1)");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.DayOfWeek).HasColumnType("tinyint");
        builder.Property(s => s.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(s => s.RowVersion).IsRowVersion();
    }
}
