using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.Core.Orders.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", table =>
        {
            table.HasCheckConstraint("CK_OrderItems_Quantity", "[Quantity] BETWEEN 1 AND 99");
            table.HasCheckConstraint("CK_OrderItems_Prices", "[ItemPrice] >= 0 AND [OptionsPrice] >= 0");
        });

        builder.HasKey(i => i.Id);
        builder.Property(i => i.ItemName).HasColumnType("nvarchar(150)").IsRequired();
        builder.Property(i => i.ItemNameLocalized).HasColumnType("nvarchar(150)");
        builder.Property(i => i.ItemPrice).HasColumnType("decimal(18,0)");
        builder.Property(i => i.OptionsPrice).HasColumnType("decimal(18,0)").HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(i => i.Note).HasColumnType("nvarchar(200)");
        builder.Ignore(i => i.LineTotal);
        builder.Ignore(i => i.IsVoided);
        builder.Property(i => i.Round).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(i => i.VoidReason).HasColumnType("nvarchar(200)");

        // Món bị xóa thật (hiếm, món chỉ xóa mềm) thì dòng đơn giữ tên/giá đã chụp
        builder.HasOne<MenuItem>().WithMany().HasForeignKey(i => i.MenuItemId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(i => i.Options).WithOne().HasForeignKey(o => o.OrderItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(i => i.OrderId);
        builder.HasIndex(i => i.MenuItemId);
    }
}
