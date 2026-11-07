using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class StaticPageVersionConfiguration : IEntityTypeConfiguration<StaticPageVersion>
{
    public void Configure(EntityTypeBuilder<StaticPageVersion> builder)
    {
        builder.ToTable("StaticPageVersions");
        builder.HasKey(v => new { v.PageId, v.Version });
        builder.Property(v => v.Title).HasColumnType("nvarchar(200)").IsRequired();
        builder.Property(v => v.ContentHtml).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasOne<StaticPage>().WithMany().HasForeignKey(v => v.PageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Staff>().WithMany().HasForeignKey(v => v.UpdatedByStaffId).OnDelete(DeleteBehavior.NoAction);
    }
}
