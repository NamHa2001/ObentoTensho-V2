using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderNoPickup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AttentionNotifiedAtUtc",
                table: "Orders",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NoPickup",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerPhone_NoPickup",
                table: "Orders",
                columns: new[] { "CustomerPhone", "NoPickup" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_CustomerPhone_NoPickup",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AttentionNotifiedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "NoPickup",
                table: "Orders");
        }
    }
}
