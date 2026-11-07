using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class StaticPageConfiguration : IEntityTypeConfiguration<StaticPage>
{
    public void Configure(EntityTypeBuilder<StaticPage> builder)
    {
        builder.ToTable("StaticPages");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Slug).HasColumnType("varchar(100)").IsRequired();
        builder.Property(p => p.Title).HasColumnType("nvarchar(200)").IsRequired();
        builder.Property(p => p.ContentHtml).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(p => p.RowVersion).IsRowVersion();
        builder.HasOne<Staff>().WithMany().HasForeignKey(p => p.UpdatedByStaffId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(p => p.Slug).IsUnique();
    }
}
