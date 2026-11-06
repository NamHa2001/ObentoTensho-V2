using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Infrastructure.Messaging;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", table =>
            table.HasCheckConstraint("CK_OutboxMessages_Attempts", "[Attempts] >= 0"));

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).HasColumnType("varchar(100)").IsRequired();
        builder.Property(m => m.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(m => m.IdempotencyKey).HasColumnType("varchar(150)").IsRequired();
        builder.Property(m => m.LastError).HasColumnType("nvarchar(500)");

        builder.HasIndex(m => m.IdempotencyKey).IsUnique();

        // PERF-53: bộ xử lý chỉ quét tin còn chờ, theo thứ tự tới hạn
        builder.HasIndex(m => new { m.NextAttemptAtUtc, m.Id })
            .HasDatabaseName("IX_OutboxMessages_Pending")
            .HasFilter("[ProcessedAtUtc] IS NULL AND [FailedAtUtc] IS NULL");
    }
}
