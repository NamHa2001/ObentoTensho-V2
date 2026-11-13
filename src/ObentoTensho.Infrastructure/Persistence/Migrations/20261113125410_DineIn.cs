using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DineIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "Channel",
                table: "Orders",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<int>(
                name: "GuestCount",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TableId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Round",
                table: "OrderItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAtUtc",
                table: "OrderItems",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ServedAtUtc",
                table: "OrderItems",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "OrderItems",
                type: "nvarchar(200)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAtUtc",
                table: "OrderItems",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedByStaffId",
                table: "OrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Orders_OpenTable",
                table: "Orders",
                column: "TableId",
                unique: true,
                filter: "[Channel] = 1 AND [Status] IN (0, 1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Channel",
                table: "Orders",
                sql: "[Channel] BETWEEN 0 AND 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_DineIn",
                table: "Orders",
                sql: "[Channel] <> 1 OR ([GuestCount] BETWEEN 1 AND 50)");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Tables_TableId",
                table: "Orders",
                column: "TableId",
                principalTable: "Tables",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Tables_TableId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "UX_Orders_OpenTable",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Channel",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_DineIn",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GuestCount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TableId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Round",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SentAtUtc",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ServedAtUtc",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "VoidedAtUtc",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "VoidedByStaffId",
                table: "OrderItems");
        }
    }
}
