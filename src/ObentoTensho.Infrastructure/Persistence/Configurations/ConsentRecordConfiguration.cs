using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("ConsentRecords");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Email).HasColumnType("varchar(150)");
        builder.Property(c => c.DocType).HasColumnType("varchar(30)").IsRequired();
        builder.Property(c => c.Version).HasColumnType("varchar(20)").IsRequired();
        builder.Property(c => c.Ip).HasColumnType("varchar(45)");
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(c => new { c.Email, c.DocType });
        builder.HasIndex(c => new { c.UserId, c.DocType });
    }
}
