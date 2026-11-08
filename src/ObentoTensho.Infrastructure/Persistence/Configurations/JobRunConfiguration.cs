using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Infrastructure.Jobs;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class JobRunConfiguration : IEntityTypeConfiguration<JobRun>
{
    public void Configure(EntityTypeBuilder<JobRun> builder)
    {
        builder.ToTable("JobRuns");
        builder.HasKey(r => r.Name);
        builder.Property(r => r.Name).HasColumnType("varchar(100)");
        builder.Property(r => r.LastRunLocalDate).HasColumnType("date");
    }
}
