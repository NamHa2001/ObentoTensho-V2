using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Delivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Amounts",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Channel",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddress",
                table: "Orders",
                type: "nvarchar(300)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryDistanceKm",
                table: "Orders",
                type: "decimal(6,1)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "Orders",
                type: "decimal(18,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Amounts",
                table: "Orders",
                sql: "[SubTotal] >= 0 AND [MemberDiscount] >= 0 AND [VoucherDiscount] >= 0 AND [MemberDiscount] + [VoucherDiscount] <= [SubTotal] AND [TotalAmount] >= 0 AND [DeliveryFee] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Channel",
                table: "Orders",
                sql: "[Channel] BETWEEN 0 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Delivery",
                table: "Orders",
                sql: "[Channel] <> 2 OR [DeliveryAddress] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders",
                sql: "[Status] BETWEEN 0 AND 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Amounts",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Channel",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Delivery",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryAddress",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryDistanceKm",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                table: "Orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Amounts",
                table: "Orders",
                sql: "[SubTotal] >= 0 AND [MemberDiscount] >= 0 AND [VoucherDiscount] >= 0 AND [MemberDiscount] + [VoucherDiscount] <= [SubTotal] AND [TotalAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Channel",
                table: "Orders",
                sql: "[Channel] BETWEEN 0 AND 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders",
                sql: "[Status] BETWEEN 0 AND 4");
        }
    }
}
