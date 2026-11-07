using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Vouchers.Domain;
using ObentoTensho.Infrastructure.Persistence.Conversions;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        var maxStatus = (byte)Enum.GetValues<OrderStatus>().Max();
        var maxMethod = (byte)Enum.GetValues<PaymentMethod>().Max();
        var maxPayment = (byte)Enum.GetValues<PaymentStatus>().Max();
        var actors = string.Join(", ", SnakeCaseEnum.AllValues<OrderActor>().Select(v => $"'{v}'"));

        builder.ToTable("Orders", table =>
        {
            table.HasCheckConstraint("CK_Orders_Status", $"[Status] BETWEEN 0 AND {maxStatus}");
            table.HasCheckConstraint("CK_Orders_PaymentMethod", $"[PaymentMethod] BETWEEN 0 AND {maxMethod}");
            table.HasCheckConstraint("CK_Orders_PaymentStatus", $"[PaymentStatus] BETWEEN 0 AND {maxPayment}");

            // BR-ORD-04: giảm không âm, không vượt tạm tính; tổng tiền không âm
            table.HasCheckConstraint(
                "CK_Orders_Amounts",
                "[SubTotal] >= 0 AND [MemberDiscount] >= 0 AND [VoucherDiscount] >= 0 AND [MemberDiscount] + [VoucherDiscount] <= [SubTotal] AND [TotalAmount] >= 0");

            // BR-ORD-10: hoàn thành thì đã thanh toán
            table.HasCheckConstraint("CK_Orders_CompletedPaid", "[Status] <> 3 OR [PaymentStatus] = 1");
            table.HasCheckConstraint("CK_Orders_CancelledByType", $"[CancelledByType] IS NULL OR [CancelledByType] IN ({actors})");
        });

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
        builder.Property(o => o.CancelReason).HasColumnType("nvarchar(300)");
        builder.Property(o => o.CancelledByType).HasConversion(new SnakeCaseEnumConverter<OrderActor>()).HasColumnType("varchar(10)");
        builder.Property(o => o.RefundNote).HasColumnType("nvarchar(300)");
        builder.Property(o => o.RowVersion).IsRowVersion();
        builder.Ignore(o => o.ReadyForKitchen);
        builder.Ignore(o => o.RefundPending);

        builder.HasOne<User>().WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Voucher>().WithMany().HasForeignKey(o => o.VoucherId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);

        ConfigureIndexes(builder);
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
    }
}
