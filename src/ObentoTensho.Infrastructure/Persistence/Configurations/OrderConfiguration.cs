using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Tables.Domain;
using ObentoTensho.Core.Vouchers.Domain;
using ObentoTensho.Infrastructure.Persistence.Conversions;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        ConfigureChecks(builder);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.OrderCode).HasColumnType("varchar(25)").IsRequired();
        builder.Property(o => o.AccessTokenHash).HasColumnType("char(64)").IsRequired();
        builder.Property(o => o.CustomerName).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(o => o.CustomerPhone).HasColumnType("varchar(15)").IsRequired();
        builder.Property(o => o.CustomerEmail).HasColumnType("varchar(150)").IsRequired();
        builder.Property(o => o.Lang).HasColumnType("char(2)").IsRequired();
        builder.Property(o => o.Note).HasColumnType("nvarchar(500)");
        builder.Property(o => o.SubTotal).HasColumnType("decimal(18,0)");
        builder.Property(o => o.MemberDiscount).HasColumnType("decimal(18,0)").HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(o => o.VoucherDiscount).HasColumnType("decimal(18,0)").HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(o => o.TotalAmount).HasColumnType("decimal(18,0)");
        builder.Property(o => o.VoucherCodeSnapshot).HasColumnType("varchar(50)");
        builder.Property(o => o.PaymentMethod).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(o => o.PaymentStatus).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(o => o.Status).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(o => o.DeliveryAddress).HasColumnType("nvarchar(300)");
        builder.Property(o => o.DeliveryDistanceKm).HasColumnType("decimal(6,1)");
        builder.Property(o => o.DeliveryFee).HasColumnType("decimal(18,0)").HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(o => o.ShippingDiscount).HasColumnType("decimal(18,0)").HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(o => o.Channel).HasConversion<byte>().HasColumnType("tinyint").HasDefaultValue(OrderChannel.Pickup).ValueGeneratedNever();
        builder.Property(o => o.CancelReason).HasColumnType("nvarchar(300)");
        builder.Property(o => o.CancelledByType).HasConversion(new SnakeCaseEnumConverter<OrderActor>()).HasColumnType("varchar(10)");
        builder.Property(o => o.RefundNote).HasColumnType("nvarchar(300)");
        builder.Property(o => o.RowVersion).IsRowVersion();
        builder.Ignore(o => o.ReadyForKitchen);
        builder.Ignore(o => o.RefundPending);

        builder.HasOne<User>().WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Voucher>().WithMany().HasForeignKey(o => o.VoucherId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<DiningTable>().WithMany().HasForeignKey(o => o.TableId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);

        ConfigureIndexes(builder);
    }

    private static void ConfigureChecks(EntityTypeBuilder<Order> builder)
    {
        var maxStatus = (byte)Enum.GetValues<OrderStatus>().Max();
        var maxMethod = (byte)Enum.GetValues<PaymentMethod>().Max();
        var maxPayment = (byte)Enum.GetValues<PaymentStatus>().Max();
        var maxChannel = (byte)Enum.GetValues<OrderChannel>().Max();
        var actors = string.Join(", ", SnakeCaseEnum.AllValues<OrderActor>().Select(v => $"'{v}'"));

        builder.ToTable("Orders", table =>
        {
            table.HasCheckConstraint("CK_Orders_Status", $"[Status] BETWEEN 0 AND {maxStatus}");
            table.HasCheckConstraint("CK_Orders_PaymentMethod", $"[PaymentMethod] BETWEEN 0 AND {maxMethod}");
            table.HasCheckConstraint("CK_Orders_PaymentStatus", $"[PaymentStatus] BETWEEN 0 AND {maxPayment}");
            table.HasCheckConstraint("CK_Orders_Channel", $"[Channel] BETWEEN 0 AND {maxChannel}");
            // Tại bàn: phải có bàn lúc mở và số khách 1–50
            table.HasCheckConstraint("CK_Orders_DineIn", "[Channel] <> 1 OR ([GuestCount] BETWEEN 1 AND 50)");

            // BR-ORD-04: giảm không âm, không vượt tạm tính; tổng tiền không âm
            table.HasCheckConstraint(
                "CK_Orders_Amounts",
                "[SubTotal] >= 0 AND [MemberDiscount] >= 0 AND [VoucherDiscount] >= 0 AND [MemberDiscount] + [VoucherDiscount] <= [SubTotal] AND [TotalAmount] >= 0 AND [DeliveryFee] >= 0 AND [ShippingDiscount] >= 0 AND [ShippingDiscount] <= [DeliveryFee]");
            // Giao hàng: phải có địa chỉ
            table.HasCheckConstraint("CK_Orders_Delivery", "[Channel] <> 2 OR [DeliveryAddress] IS NOT NULL");

            // BR-ORD-10: hoàn thành thì đã thanh toán
            table.HasCheckConstraint("CK_Orders_CompletedPaid", "[Status] <> 3 OR [PaymentStatus] = 1");
            table.HasCheckConstraint("CK_Orders_CancelledByType", $"[CancelledByType] IS NULL OR [CancelledByType] IN ({actors})");
        });
    }

    private static void ConfigureIndexes(EntityTypeBuilder<Order> builder)
    {
        builder.HasIndex(o => o.OrderCode).IsUnique();

        // docs/srs/12 §10: danh sách theo trạng thái, màn hình bếp theo giờ lấy
        builder.HasIndex(o => new { o.Status, o.CreatedAtUtc });
        builder.HasIndex(o => new { o.Status, o.PickupAtUtc });

        // BR-ORD-20/21: đếm theo SĐT chuẩn hóa
        builder.HasIndex(o => new { o.CustomerPhone, o.Status });
        builder.HasIndex(o => new { o.CustomerPhone, o.NoPickup });
        builder.HasIndex(o => o.UserId);
        // Một bàn chỉ một phiếu tại bàn đang mở (hai nhân viên mở cùng lúc: một bên nhận TABLE_CONFLICT)
        builder.HasIndex(o => o.TableId, "UX_Orders_OpenTable").IsUnique().HasFilter("[Channel] = 1 AND [Status] IN (0, 1, 2)");
    }
}
