using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.Core.Orders.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        var maxStatus = (byte)Enum.GetValues<ReviewStatus>().Max();
        builder.ToTable("Reviews", table =>
        {
            table.HasCheckConstraint("CK_Reviews_Rating", "[Rating] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_Reviews_Status", $"[Status] BETWEEN 0 AND {maxStatus}");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Rating).HasColumnType("tinyint");
        builder.Property(r => r.Comment).HasColumnType("nvarchar(1000)");
        builder.Property(r => r.ReplyContent).HasColumnType("nvarchar(1000)");
        builder.Property(r => r.Status).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Order>().WithMany().HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<MenuItem>().WithMany().HasForeignKey(r => r.MenuItemId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Staff>().WithMany().HasForeignKey(r => r.RepliedByStaffId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Staff>().WithMany().HasForeignKey(r => r.ReviewedByStaffId).OnDelete(DeleteBehavior.NoAction);

        // BR-CNT-12. SQL Server coi NULL là một giá trị: mỗi đơn chỉ một đánh giá chung
        builder.HasIndex(r => new { r.UserId, r.OrderId, r.MenuItemId }).IsUnique();
        builder.HasIndex(r => new { r.Status, r.MenuItemId, r.Id });
        builder.HasIndex(r => new { r.UserId, r.CreatedAtUtc });
    }
}
