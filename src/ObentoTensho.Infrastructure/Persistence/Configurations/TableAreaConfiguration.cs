using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class TableAreaConfiguration : IEntityTypeConfiguration<TableArea>
{
    public void Configure(EntityTypeBuilder<TableArea> builder)
    {
        builder.ToTable("TableAreas");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(a => a.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(a => a.RowVersion).IsRowVersion();
        builder.HasIndex(a => a.Name).IsUnique();
    }
}
