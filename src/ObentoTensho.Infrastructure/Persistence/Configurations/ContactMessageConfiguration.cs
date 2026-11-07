using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> builder)
    {
        builder.ToTable("ContactMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.FullName).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(m => m.Email).HasColumnType("varchar(150)").IsRequired();
        builder.Property(m => m.PhoneNumber).HasColumnType("varchar(20)");
        builder.Property(m => m.IpAddress).HasColumnType("varchar(45)");
        builder.Property(m => m.Subject).HasColumnType("nvarchar(200)");
        builder.Property(m => m.Message).HasColumnType("nvarchar(2000)").IsRequired();
        builder.HasOne<Staff>().WithMany().HasForeignKey(m => m.ReadByStaffId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Staff>().WithMany().HasForeignKey(m => m.RepliedByStaffId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(m => new { m.Email, m.CreatedAtUtc });
        builder.HasIndex(m => new { m.IsRead, m.Id });
    }
}
