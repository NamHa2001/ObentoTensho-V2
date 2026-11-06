using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Notifications.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        var maxStatus = (byte)Enum.GetValues<EmailStatus>().Max();

        builder.ToTable("EmailLogs", table =>
        {
            table.HasCheckConstraint("CK_EmailLogs_Status", $"[Status] BETWEEN 0 AND {maxStatus}");
            table.HasCheckConstraint("CK_EmailLogs_Attempts", "[Attempts] >= 0");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Type).HasColumnType("varchar(50)").IsRequired();
        builder.Property(l => l.ToEmail).HasColumnType("varchar(150)").IsRequired();
        builder.Property(l => l.Status).HasConversion<byte>().HasColumnType("tinyint")
            .HasDefaultValue(EmailStatus.Pending).ValueGeneratedNever();
        builder.Property(l => l.LastError).HasColumnType("nvarchar(500)");
        builder.Property(l => l.RelatedEntityId).HasColumnType("varchar(100)");
        builder.Property(l => l.IdempotencyKey).HasColumnType("varchar(150)").IsRequired();

        builder.HasIndex(l => l.IdempotencyKey).IsUnique();

        // BR-NTF-08 / BR-NTF-12: đếm thư đã gửi theo địa chỉ và theo ngày
        builder.HasIndex(l => new { l.ToEmail, l.SentAtUtc }).HasFilter("[Status] = 1");
        builder.HasIndex(l => l.SentAtUtc).HasFilter("[Status] = 1");
    }
}
