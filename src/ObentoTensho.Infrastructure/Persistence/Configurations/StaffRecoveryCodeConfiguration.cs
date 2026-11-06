using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class StaffRecoveryCodeConfiguration : IEntityTypeConfiguration<StaffRecoveryCode>
{
    public void Configure(EntityTypeBuilder<StaffRecoveryCode> builder)
    {
        builder.ToTable("StaffRecoveryCodes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.CodeHash).HasColumnType("char(64)").IsRequired();

        builder.HasOne<Staff>().WithMany().HasForeignKey(c => c.StaffId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.StaffId, c.CodeHash }).IsUnique();
    }
}
