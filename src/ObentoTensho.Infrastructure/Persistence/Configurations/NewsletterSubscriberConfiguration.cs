using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class NewsletterSubscriberConfiguration : IEntityTypeConfiguration<NewsletterSubscriber>
{
    public void Configure(EntityTypeBuilder<NewsletterSubscriber> builder)
    {
        builder.ToTable("NewsletterSubscribers");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Email).HasColumnType("varchar(150)").IsRequired();
        builder.Property(s => s.Lang).HasColumnType("char(2)").HasDefaultValue("vi").ValueGeneratedNever();
        builder.Property(s => s.ConfirmTokenHash).HasColumnType("char(64)");
        builder.Property(s => s.UnsubscribeTokenHash).HasColumnType("char(64)").IsRequired();
        builder.Property(s => s.UnsubscribeTokenProtected).HasColumnType("varbinary(512)").IsRequired();
        builder.Ignore(s => s.IsSubscribed);
        builder.HasIndex(s => s.Email).IsUnique();
        builder.HasIndex(s => s.UnsubscribeTokenHash).IsUnique();
        builder.HasIndex(s => s.ConfirmTokenHash);
    }
}
