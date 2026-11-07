using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Settings.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("SystemSettings");
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasColumnType("varchar(100)");
        builder.Property(s => s.Value).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(s => s.ValueType).HasConversion<string>().HasColumnType("varchar(10)").IsRequired();
        builder.Property(s => s.MinValue).HasColumnType("decimal(18,2)");
        builder.Property(s => s.MaxValue).HasColumnType("decimal(18,2)");
        builder.Property(s => s.Description).HasColumnType("nvarchar(500)").IsRequired();
        builder.HasOne<Staff>().WithMany().HasForeignKey(s => s.UpdatedByStaffId).OnDelete(DeleteBehavior.NoAction);
    }
}
