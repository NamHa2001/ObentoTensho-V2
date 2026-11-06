using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class OpeningHourConfiguration : IEntityTypeConfiguration<OpeningHour>
{
    public void Configure(EntityTypeBuilder<OpeningHour> builder)
    {
        builder.ToTable("OpeningHours", table =>
        {
            table.HasCheckConstraint("CK_OpeningHours_DayOfWeek", "[DayOfWeek] BETWEEN 0 AND 6");

            // BR-TBL-12: Mở < Đóng, trừ ngày nghỉ hoặc khoảng qua nửa đêm (khi đó Đóng < Mở)
            table.HasCheckConstraint(
                "CK_OpeningHours_Range",
                "[IsClosed] = 1 OR ([CrossesMidnight] = 0 AND [OpenTime] < [CloseTime]) OR ([CrossesMidnight] = 1 AND [CloseTime] < [OpenTime])");
        });

        builder.HasKey(h => h.Id);
        builder.Property(h => h.DayOfWeek).HasColumnType("tinyint");
        builder.Property(h => h.RowVersion).IsRowVersion();
        builder.HasIndex(h => new { h.DayOfWeek, h.OpenTime }).IsUnique();
    }
}
