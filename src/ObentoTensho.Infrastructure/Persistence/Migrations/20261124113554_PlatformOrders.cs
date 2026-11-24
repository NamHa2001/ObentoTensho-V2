using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Channel",
                table: "Orders");

            migrationBuilder.AddColumn<decimal>(
                name: "DeductionAmount",
                table: "Orders",
                type: "decimal(18,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeductionPercent",
                table: "Orders",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalOrderCode",
                table: "Orders",
                type: "nvarchar(50)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetRevenue",
                table: "Orders",
                type: "decimal(18,0)",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "PaymentGroup",
                table: "Orders",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<decimal>(
                name: "PitAmount",
                table: "Orders",
                type: "decimal(18,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PitPercent",
                table: "Orders",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Platform",
                table: "Orders",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformDiscount",
                table: "Orders",
                type: "decimal(18,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformDiscountPercent",
                table: "Orders",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedAmount",
                table: "Orders",
                type: "decimal(18,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "Orders",
                type: "decimal(18,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatPercent",
                table: "Orders",
                type: "decimal(5,2)",
                nullable: true);

            // docs/srs/23 BR-PLT-04: đơn đã hoàn thành trước khi có tính năng được chụp theo tỉ lệ mặc định 25% / 3% / 1,5%
            migrationBuilder.Sql("""
                UPDATE o
                SET [NetRevenue] = n.Net,
                    [DeductionPercent] = 25, [VatPercent] = 3, [PitPercent] = 1.5,
                    [DeductionAmount] = ROUND(n.Net * 0.25, 0), [VatAmount] = ROUND(n.Net * 0.03, 0), [PitAmount] = ROUND(n.Net * 0.015, 0),
                    [ReceivedAmount] = n.Net - ROUND(n.Net * 0.25, 0) - ROUND(n.Net * 0.03, 0) - ROUND(n.Net * 0.015, 0)
                FROM [Orders] o
                CROSS APPLY (SELECT CASE WHEN o.[SubTotal] - o.[MemberDiscount] - o.[VoucherDiscount] < 0 THEN 0 ELSE o.[SubTotal] - o.[MemberDiscount] - o.[VoucherDiscount] END AS Net) n
                WHERE o.[Status] = 3 AND o.[NetRevenue] IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_CompletedAtUtc",
                table: "Orders",
                columns: new[] { "Status", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Orders_ExternalCode",
                table: "Orders",
                columns: new[] { "PaymentGroup", "Platform", "ExternalOrderCode" },
                unique: true,
                filter: "[ExternalOrderCode] IS NOT NULL AND [Status] <> 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Channel",
                table: "Orders",
                sql: "[Channel] BETWEEN 0 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_PaymentGroup",
                table: "Orders",
                sql: "[PaymentGroup] BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Platform",
                table: "Orders",
                sql: "[Platform] IS NULL OR [Platform] BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_PlatformDiscount",
                table: "Orders",
                sql: "[PlatformDiscount] >= 0 AND [PlatformDiscount] <= [SubTotal]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_PlatformOrder",
                table: "Orders",
                sql: "([PaymentGroup] = 0 AND [Platform] IS NULL AND [ExternalOrderCode] IS NULL) OR ([PaymentGroup] <> 0 AND [Platform] IS NOT NULL AND [ExternalOrderCode] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_Status_CompletedAtUtc",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "UX_Orders_ExternalCode",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Channel",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_PaymentGroup",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Platform",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_PlatformDiscount",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_PlatformOrder",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeductionAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeductionPercent",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ExternalOrderCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "NetRevenue",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentGroup",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PitAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PitPercent",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Platform",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PlatformDiscount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PlatformDiscountPercent",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReceivedAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VatPercent",
                table: "Orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Channel",
                table: "Orders",
                sql: "[Channel] BETWEEN 0 AND 3");
        }
    }
}
