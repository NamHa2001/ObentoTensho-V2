using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuyerType = table.Column<byte>(type: "tinyint", nullable: false),
                    BuyerName = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    NationalId = table.Column<string>(type: "varchar(12)", nullable: true),
                    TaxCode = table.Column<string>(type: "varchar(14)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    Email = table.Column<string>(type: "varchar(150)", nullable: true),
                    Phone = table.Column<string>(type: "varchar(15)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "varchar(50)", nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IssuedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceRequests", x => x.Id);
                    table.CheckConstraint("CK_InvoiceRequests_Buyer", "([BuyerType] = 0 AND [NationalId] IS NOT NULL) OR ([BuyerType] = 1 AND [TaxCode] IS NOT NULL)");
                    table.CheckConstraint("CK_InvoiceRequests_BuyerType", "[BuyerType] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_InvoiceRequests_Issued", "[Status] <> 1 OR [InvoiceNumber] IS NOT NULL");
                    table.CheckConstraint("CK_InvoiceRequests_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_InvoiceRequests_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRequests_Status_CreatedAtUtc",
                table: "InvoiceRequests",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_InvoiceRequests_ActiveOrder",
                table: "InvoiceRequests",
                column: "OrderId",
                unique: true,
                filter: "[Status] <> 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceRequests");
        }
    }
}
