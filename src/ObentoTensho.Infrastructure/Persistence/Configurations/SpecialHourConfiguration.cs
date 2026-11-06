using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class SpecialHourConfiguration : IEntityTypeConfiguration<SpecialHour>
{
    public void Configure(EntityTypeBuilder<SpecialHour> builder)
    {
        builder.ToTable("SpecialHours", table => table.HasCheckConstraint(
            "CK_SpecialHours_Range",
            "([IsClosed] = 1 AND [OpenTime] IS NULL AND [CloseTime] IS NULL) OR ([IsClosed] = 0 AND [OpenTime] < [CloseTime])"));

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Note).HasColumnType("nvarchar(200)");
        builder.HasOne<Staff>().WithMany().HasForeignKey(h => h.CreatedByStaffId).OnDelete(DeleteBehavior.NoAction);

        // NULL được coi là bằng nhau trong chỉ mục duy nhất của SQL Server ⇒ mỗi ngày tối đa một dòng "đóng cửa"
        builder.HasIndex(h => new { h.Date, h.OpenTime }).IsUnique();
    }
}
