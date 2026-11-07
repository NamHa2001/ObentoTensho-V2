using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class NewsCategoryConfiguration : IEntityTypeConfiguration<NewsCategory>
{
    public void Configure(EntityTypeBuilder<NewsCategory> builder)
    {
        builder.ToTable("NewsCategories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(c => c.Slug).HasColumnType("varchar(150)").IsRequired();
        builder.Property(c => c.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(c => c.RowVersion).IsRowVersion();
        builder.HasIndex(c => c.Slug).IsUnique();
    }
}
