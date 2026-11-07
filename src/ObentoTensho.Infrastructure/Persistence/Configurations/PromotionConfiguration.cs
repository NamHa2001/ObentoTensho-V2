using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Promotions.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("Promotions", table => table.HasCheckConstraint("CK_Promotions_Dates", "[EndDate] >= [StartDate]"));

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Title).HasColumnType("nvarchar(200)").IsRequired();
        builder.Property(p => p.Slug).HasColumnType("varchar(200)").IsRequired();
        builder.Property(p => p.DescriptionHtml).HasColumnType("nvarchar(max)");
        builder.Property(p => p.StartDate).HasColumnType("date");
        builder.Property(p => p.EndDate).HasColumnType("date");
        builder.Property(p => p.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasOne<Staff>().WithMany().HasForeignKey(p => p.CreatedByStaffId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => new { p.IsActive, p.EndDate });
    }
}
