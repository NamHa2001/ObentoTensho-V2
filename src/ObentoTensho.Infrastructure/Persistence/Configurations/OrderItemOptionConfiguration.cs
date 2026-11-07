using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.Core.Orders.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class OrderItemOptionConfiguration : IEntityTypeConfiguration<OrderItemOption>
{
    public void Configure(EntityTypeBuilder<OrderItemOption> builder)
    {
        builder.ToTable("OrderItemOptions", table =>
            table.HasCheckConstraint("CK_OrderItemOptions_PriceDelta", "[PriceDelta] >= 0"));

        builder.HasKey(o => o.Id);
        builder.Property(o => o.GroupName).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(o => o.OptionName).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(o => o.GroupNameLocalized).HasColumnType("nvarchar(100)");
        builder.Property(o => o.OptionNameLocalized).HasColumnType("nvarchar(100)");
        builder.Property(o => o.PriceDelta).HasColumnType("decimal(18,0)");

        // BR-MNU-23: xóa tùy chọn không ảnh hưởng đơn cũ — dòng giữ tên/giá đã chụp
        builder.HasOne<MenuOption>().WithMany().HasForeignKey(o => o.OptionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(o => o.OrderItemId);
        builder.HasIndex(o => o.OptionId);
    }
}
