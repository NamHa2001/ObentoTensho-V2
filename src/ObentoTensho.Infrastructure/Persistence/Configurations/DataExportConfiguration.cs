using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Customers.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class DataExportConfiguration : IEntityTypeConfiguration<DataExport>
{
    public void Configure(EntityTypeBuilder<DataExport> builder)
    {
        builder.ToTable("DataExports");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(e => e.Status).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(e => e.Reason).HasColumnType("nvarchar(300)").IsRequired();
        builder.Property(e => e.Content).HasColumnType("varbinary(max)");
        builder.Property(e => e.DownloadTokenHash).HasColumnType("char(64)");
        builder.HasOne<Staff>().WithMany().HasForeignKey(e => e.RequestedByStaffId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(e => new { e.RequestedByStaffId, e.Kind, e.CreatedAtUtc });
        builder.HasIndex(e => new { e.Status, e.CreatedAtUtc });
        builder.HasIndex(e => e.DownloadTokenHash);
    }
}
