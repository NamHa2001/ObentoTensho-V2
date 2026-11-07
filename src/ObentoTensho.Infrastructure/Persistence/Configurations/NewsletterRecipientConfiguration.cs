using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class NewsletterRecipientConfiguration : IEntityTypeConfiguration<NewsletterRecipient>
{
    public void Configure(EntityTypeBuilder<NewsletterRecipient> builder)
    {
        var maxStatus = (byte)Enum.GetValues<NewsletterRecipientStatus>().Max();
        builder.ToTable("NewsletterRecipients", table => table.HasCheckConstraint("CK_NewsletterRecipients_Status", $"[Status] BETWEEN 0 AND {maxStatus}"));
        builder.HasKey(r => new { r.CampaignId, r.SubscriberId });
        builder.Property(r => r.Status).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(r => r.Attempts).HasColumnType("tinyint");
        builder.Property(r => r.Error).HasColumnType("nvarchar(300)");
        builder.HasOne<NewsletterCampaign>().WithMany().HasForeignKey(r => r.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<NewsletterSubscriber>().WithMany().HasForeignKey(r => r.SubscriberId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(r => new { r.CampaignId, r.Status });
    }
}
