using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Infrastructure.Persistence.Conversions;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        var actors = string.Join(", ", SnakeCaseEnum.AllValues<AuditActorType>().Select(v => $"'{v}'"));

        builder.ToTable("AuditLogs", table => table.HasCheckConstraint("CK_AuditLogs_ActorType", $"[ActorType] IN ({actors})"));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ActorType).HasConversion(new SnakeCaseEnumConverter<AuditActorType>()).HasColumnType("varchar(10)");
        builder.Property(a => a.Action).HasColumnType("nvarchar(200)").IsRequired();
        builder.Property(a => a.EntityType).HasColumnType("varchar(50)").IsRequired();
        builder.Property(a => a.EntityId).HasColumnType("varchar(100)").IsRequired();
        builder.Property(a => a.OldDataJson).HasColumnType("nvarchar(max)");
        builder.Property(a => a.NewDataJson).HasColumnType("nvarchar(max)");
        builder.Property(a => a.IpAddress).HasColumnType("varchar(45)");

        builder.HasIndex(a => new { a.ActorId, a.CreatedAtUtc }).IsDescending(false, true);
        builder.HasIndex(a => new { a.EntityType, a.CreatedAtUtc }).IsDescending(false, true);
    }
}
