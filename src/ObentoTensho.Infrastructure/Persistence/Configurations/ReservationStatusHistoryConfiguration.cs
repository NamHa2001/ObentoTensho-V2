using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Reservations.Domain;
using ObentoTensho.Infrastructure.Persistence.Conversions;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class ReservationStatusHistoryConfiguration : IEntityTypeConfiguration<ReservationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ReservationStatusHistory> builder)
    {
        var actors = string.Join(", ", SnakeCaseEnum.AllValues<ReservationActor>().Select(v => $"'{v}'"));

        builder.ToTable("ReservationStatusHistory", table =>
            table.HasCheckConstraint("CK_ReservationStatusHistory_ChangedByType", $"[ChangedByType] IN ({actors})"));

        builder.HasKey(h => h.Id);
        builder.Property(h => h.FromStatus).HasConversion<byte?>().HasColumnType("tinyint");
        builder.Property(h => h.ToStatus).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(h => h.ChangedByType).HasConversion(new SnakeCaseEnumConverter<ReservationActor>()).HasColumnType("varchar(10)");
        builder.Property(h => h.Reason).HasColumnType("nvarchar(300)");

        builder.HasOne<Reservation>().WithMany().HasForeignKey(h => h.ReservationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(h => h.ReservationId);
    }
}
