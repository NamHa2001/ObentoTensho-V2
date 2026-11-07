using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class NewsletterCampaignConfiguration : IEntityTypeConfiguration<NewsletterCampaign>
{
    public void Configure(EntityTypeBuilder<NewsletterCampaign> builder)
    {
        var maxStatus = (byte)Enum.GetValues<NewsletterCampaignStatus>().Max();
        builder.ToTable("NewsletterCampaigns", table => table.HasCheckConstraint("CK_NewsletterCampaigns_Status", $"[Status] BETWEEN 0 AND {maxStatus}"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Subject).HasColumnType("nvarchar(300)").IsRequired();
        builder.Property(c => c.ContentHtml).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(c => c.Status).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(c => c.RowVersion).IsRowVersion();
        builder.HasOne<Staff>().WithMany().HasForeignKey(c => c.SentByStaffId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(c => new { c.Status, c.ScheduledAtUtc });
    }
}
