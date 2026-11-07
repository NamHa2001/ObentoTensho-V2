using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Orders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderCode = table.Column<string>(type: "varchar(25)", nullable: false),
                    DailySeq = table.Column<int>(type: "int", nullable: false),
                    AccessTokenHash = table.Column<string>(type: "char(64)", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    CustomerPhone = table.Column<string>(type: "varchar(15)", nullable: false),
                    CustomerEmail = table.Column<string>(type: "varchar(150)", nullable: false),
                    Lang = table.Column<string>(type: "char(2)", nullable: false),
                    PickupAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,0)", nullable: false),
                    MemberDiscount = table.Column<decimal>(type: "decimal(18,0)", nullable: false, defaultValue: 0m),
                    VoucherDiscount = table.Column<decimal>(type: "decimal(18,0)", nullable: false, defaultValue: 0m),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,0)", nullable: false),
                    VoucherId = table.Column<int>(type: "int", nullable: true),
                    VoucherCodeSnapshot = table.Column<string>(type: "varchar(50)", nullable: true),
                    PaymentMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    PaymentStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    NeedsContactConfirm = table.Column<bool>(type: "bit", nullable: false),
                    ContactConfirmedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ContactConfirmedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    CancelledByType = table.Column<string>(type: "varchar(10)", nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    PaidByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RefundedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RefundNote = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    RefundedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PointsEarned = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_Amounts", "[SubTotal] >= 0 AND [MemberDiscount] >= 0 AND [VoucherDiscount] >= 0 AND [MemberDiscount] + [VoucherDiscount] <= [SubTotal] AND [TotalAmount] >= 0");
                    table.CheckConstraint("CK_Orders_CancelledByType", "[CancelledByType] IS NULL OR [CancelledByType] IN ('customer', 'staff', 'system')");
                    table.CheckConstraint("CK_Orders_CompletedPaid", "[Status] <> 3 OR [PaymentStatus] = 1");
                    table.CheckConstraint("CK_Orders_PaymentMethod", "[PaymentMethod] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_Orders_PaymentStatus", "[PaymentStatus] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_Orders_Status", "[Status] BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_Orders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuItemId = table.Column<int>(type: "int", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    ItemNameLocalized = table.Column<string>(type: "nvarchar(150)", nullable: true),
                    ItemPrice = table.Column<decimal>(type: "decimal(18,0)", nullable: false),
                    OptionsPrice = table.Column<decimal>(type: "decimal(18,0)", nullable: false, defaultValue: 0m),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.CheckConstraint("CK_OrderItems_Prices", "[ItemPrice] >= 0 AND [OptionsPrice] >= 0");
                    table.CheckConstraint("CK_OrderItems_Quantity", "[Quantity] BETWEEN 1 AND 99");
                    table.ForeignKey(
                        name: "FK_OrderItems_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderStatusHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    ToStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    ChangedByType = table.Column<string>(type: "varchar(10)", nullable: false),
                    ChangedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusHistory", x => x.Id);
                    table.CheckConstraint("CK_OrderStatusHistory_ChangedByType", "[ChangedByType] IN ('customer', 'staff', 'system')");
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItemOptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    OptionId = table.Column<int>(type: "int", nullable: true),
                    GroupName = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    OptionName = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    GroupNameLocalized = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    OptionNameLocalized = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    PriceDelta = table.Column<decimal>(type: "decimal(18,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItemOptions", x => x.Id);
                    table.CheckConstraint("CK_OrderItemOptions_PriceDelta", "[PriceDelta] >= 0");
                    table.ForeignKey(
                        name: "FK_OrderItemOptions_Options_OptionId",
                        column: x => x.OptionId,
                        principalTable: "Options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrderItemOptions_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemOptions_OptionId",
                table: "OrderItemOptions",
                column: "OptionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemOptions_OrderItemId",
                table: "OrderItemOptions",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_MenuItemId",
                table: "OrderItems",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerPhone_Status",
                table: "Orders",
                columns: new[] { "CustomerPhone", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderCode",
                table: "Orders",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_CreatedAtUtc",
                table: "Orders",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_PickupAtUtc",
                table: "Orders",
                columns: new[] { "Status", "PickupAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_OrderId",
                table: "OrderStatusHistory",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderItemOptions");

            migrationBuilder.DropTable(
                name: "OrderStatusHistory");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
