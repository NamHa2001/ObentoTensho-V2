using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Invoices.Domain;
using ObentoTensho.Core.Orders.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceRequestConfiguration : IEntityTypeConfiguration<InvoiceRequest>
{
    public void Configure(EntityTypeBuilder<InvoiceRequest> builder)
    {
        var maxStatus = (byte)Enum.GetValues<InvoiceRequestStatus>().Max();
        var maxType = (byte)Enum.GetValues<InvoiceBuyerType>().Max();
        builder.ToTable("InvoiceRequests", table =>
        {
            table.HasCheckConstraint("CK_InvoiceRequests_Status", $"[Status] BETWEEN 0 AND {maxStatus}");
            table.HasCheckConstraint("CK_InvoiceRequests_BuyerType", $"[BuyerType] BETWEEN 0 AND {maxType}");
            // Cá nhân có CCCD, doanh nghiệp có MST; đã xuất thì có số hóa đơn
            table.HasCheckConstraint("CK_InvoiceRequests_Buyer", "([BuyerType] = 0 AND [NationalId] IS NOT NULL) OR ([BuyerType] = 1 AND [TaxCode] IS NOT NULL)");
            table.HasCheckConstraint("CK_InvoiceRequests_Issued", "[Status] <> 1 OR [InvoiceNumber] IS NOT NULL");
        });
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.BuyerType).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(i => i.Status).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(i => i.BuyerName).HasColumnType("nvarchar(200)").IsRequired();
        builder.Property(i => i.NationalId).HasColumnType("varchar(12)");
        builder.Property(i => i.TaxCode).HasColumnType("varchar(14)");
        builder.Property(i => i.Address).HasColumnType("nvarchar(300)");
        builder.Property(i => i.Email).HasColumnType("varchar(150)");
        builder.Property(i => i.Phone).HasColumnType("varchar(15)");
        builder.Property(i => i.Note).HasColumnType("nvarchar(300)");
        builder.Property(i => i.InvoiceNumber).HasColumnType("varchar(50)");
        builder.Property(i => i.CancelReason).HasColumnType("nvarchar(300)");
        builder.Property(i => i.RowVersion).IsRowVersion();
        builder.HasOne<Order>().WithMany().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);

        // Mỗi đơn tối đa một yêu cầu chưa hủy
        builder.HasIndex(i => i.OrderId, "UX_InvoiceRequests_ActiveOrder").IsUnique().HasFilter("[Status] <> 2");
        builder.HasIndex(i => new { i.Status, i.CreatedAtUtc });
    }
}
