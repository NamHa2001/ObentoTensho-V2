using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Reports.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class DailyStatConfiguration : IEntityTypeConfiguration<DailyStat>
{
    public void Configure(EntityTypeBuilder<DailyStat> builder)
    {
        builder.ToTable("DailyStats");
        builder.HasKey(s => new { s.Date, s.Metric, s.Dimension });
        builder.Property(s => s.Date).HasColumnType("date");
        builder.Property(s => s.Metric).HasColumnType("varchar(40)");
        builder.Property(s => s.Dimension).HasColumnType("varchar(40)").HasDefaultValue(string.Empty).ValueGeneratedNever();
        builder.Property(s => s.Value).HasColumnType("decimal(18,2)");
    }
}
