using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class SlugRedirectConfiguration : IEntityTypeConfiguration<SlugRedirect>
{
    public void Configure(EntityTypeBuilder<SlugRedirect> builder)
    {
        builder.ToTable("SlugRedirects");
        builder.HasKey(r => new { r.EntityType, r.OldSlug });
        builder.Property(r => r.EntityType).HasColumnType("varchar(20)");
        builder.Property(r => r.OldSlug).HasColumnType("varchar(300)");
        builder.HasIndex(r => new { r.EntityType, r.EntityId });
    }
}
