using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Reservations.Domain;
using ObentoTensho.Core.Tables.Domain;
using ObentoTensho.Infrastructure.Persistence.Conversions;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        var maxStatus = (byte)Enum.GetValues<ReservationStatus>().Max();
        var maxSource = (byte)Enum.GetValues<ReservationSource>().Max();
        var actors = string.Join(", ", SnakeCaseEnum.AllValues<ReservationActor>().Select(v => $"'{v}'"));

        builder.ToTable("Reservations", table =>
        {
            table.HasCheckConstraint("CK_Reservations_Status", $"[Status] BETWEEN 0 AND {maxStatus}");
            table.HasCheckConstraint("CK_Reservations_Source", $"[Source] BETWEEN 0 AND {maxSource}");
            table.HasCheckConstraint("CK_Reservations_Guests", "[AdultCount] >= 1 AND [ChildCount] >= 0");
            table.HasCheckConstraint("CK_Reservations_CancelledByType", $"[CancelledByType] IS NULL OR [CancelledByType] IN ({actors})");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.ReservationCode).HasColumnType("varchar(20)").IsRequired();
        builder.Property(r => r.AccessTokenHash).HasColumnType("char(64)");
        builder.Property(r => r.GuestName).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(r => r.GuestPhone).HasColumnType("varchar(15)").IsRequired();
        builder.Property(r => r.GuestEmail).HasColumnType("varchar(150)");
        builder.Property(r => r.ConfirmNote).HasColumnType("nvarchar(500)");
        builder.Property(r => r.CallAttempts).HasColumnType("tinyint");
        builder.Property(r => r.Source).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(r => r.Lang).HasColumnType("char(2)").IsRequired();
        builder.Property(r => r.SpecialRequest).HasColumnType("nvarchar(500)");
        builder.Property(r => r.InternalNote).HasColumnType("nvarchar(500)");
        builder.Property(r => r.Status).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(r => r.CancelReason).HasColumnType("nvarchar(300)");
        builder.Property(r => r.CancelledByType).HasConversion(new SnakeCaseEnumConverter<ReservationActor>()).HasColumnType("varchar(10)");
        builder.Property(r => r.RowVersion).IsRowVersion();
        builder.Ignore(r => r.GuestCount);
        builder.Ignore(r => r.HoldsCapacity);

        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ServiceSlot>().WithMany().HasForeignKey(r => r.SlotId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<DiningTable>().WithMany().HasForeignKey(r => r.TableId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => r.ReservationCode).IsUnique();
        builder.HasIndex(r => new { r.ReservedDate, r.Status });

        // BR-RSV-20/22: đếm theo SĐT chuẩn hóa
        builder.HasIndex(r => new { r.GuestPhone, r.Status, r.ReservedDate });
        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => new { r.TableId, r.ReservedDate });
        builder.HasIndex(r => r.SlotId);
    }
}
