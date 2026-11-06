using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class DailyCounterConfiguration : IEntityTypeConfiguration<DailyCounter>
{
    public void Configure(EntityTypeBuilder<DailyCounter> builder)
    {
        builder.ToTable("DailyCounters", table => table.HasCheckConstraint("CK_DailyCounters_LastValue", "[LastValue] >= 0"));
        builder.HasKey(c => new { c.Date, c.Kind });
        builder.Property(c => c.Kind).HasColumnType("varchar(20)");
    }
}
