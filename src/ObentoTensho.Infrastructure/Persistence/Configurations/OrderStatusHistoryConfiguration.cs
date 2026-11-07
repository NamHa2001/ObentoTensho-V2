using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Infrastructure.Persistence.Conversions;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        var actors = string.Join(", ", SnakeCaseEnum.AllValues<OrderActor>().Select(v => $"'{v}'"));

        builder.ToTable("OrderStatusHistory", table =>
            table.HasCheckConstraint("CK_OrderStatusHistory_ChangedByType", $"[ChangedByType] IN ({actors})"));

        builder.HasKey(h => h.Id);
        builder.Property(h => h.FromStatus).HasConversion<byte?>().HasColumnType("tinyint");
        builder.Property(h => h.ToStatus).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(h => h.ChangedByType).HasConversion(new SnakeCaseEnumConverter<OrderActor>()).HasColumnType("varchar(10)");
        builder.Property(h => h.Reason).HasColumnType("nvarchar(300)");

        builder.HasOne<Order>().WithMany().HasForeignKey(h => h.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(h => h.OrderId);
    }
}
