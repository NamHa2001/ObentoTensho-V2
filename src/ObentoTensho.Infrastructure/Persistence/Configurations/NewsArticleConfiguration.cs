using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Media.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class NewsArticleConfiguration : IEntityTypeConfiguration<NewsArticle>
{
    public void Configure(EntityTypeBuilder<NewsArticle> builder)
    {
        var maxStatus = (byte)Enum.GetValues<NewsStatus>().Max();
        builder.ToTable("News", table =>
        {
            table.HasCheckConstraint("CK_News_Status", $"[Status] BETWEEN 0 AND {maxStatus}");

            // Bài xuất bản luôn có mốc đăng (BR-CNT-01)
            table.HasCheckConstraint("CK_News_Published", "[Status] <> 1 OR [PublishedAtUtc] IS NOT NULL");
        });

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Title).HasColumnType("nvarchar(300)").IsRequired();
        builder.Property(n => n.Slug).HasColumnType("varchar(300)").IsRequired();
        builder.Property(n => n.TitleSearch).HasColumnType("nvarchar(400)").IsRequired();
        builder.Property(n => n.Summary).HasColumnType("nvarchar(500)");
        builder.Property(n => n.ContentHtml).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(n => n.AuthorDisplayName).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(n => n.MetaTitle).HasColumnType("nvarchar(200)");
        builder.Property(n => n.MetaDescription).HasColumnType("nvarchar(500)");
        builder.Property(n => n.Status).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(n => n.RowVersion).IsRowVersion();

        builder.HasOne<NewsCategory>().WithMany().HasForeignKey(n => n.CategoryId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Staff>().WithMany().HasForeignKey(n => n.AuthorId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<StoredImage>().WithMany().HasForeignKey(n => n.ThumbnailImageId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<StoredImage>().WithMany().HasForeignKey(n => n.OgImageId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(n => n.Slug).IsUnique();
        builder.HasIndex(n => new { n.IsDeleted, n.Status, n.PublishedAtUtc });
        builder.HasIndex(n => n.CategoryId);
        builder.HasIndex(n => n.TitleSearch);
    }
}
