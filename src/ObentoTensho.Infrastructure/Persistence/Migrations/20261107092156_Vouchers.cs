using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Vouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PromotionId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "varchar(50)", nullable: false),
                    DiscountType = table.Column<byte>(type: "tinyint", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxDiscountAmount = table.Column<decimal>(type: "decimal(18,0)", nullable: true),
                    MinOrderValue = table.Column<decimal>(type: "decimal(18,0)", nullable: false),
                    MaxUsageTotal = table.Column<int>(type: "int", nullable: true),
                    MaxUsagePerUser = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    UsedCount = table.Column<int>(type: "int", nullable: false),
                    ApplicableTier = table.Column<byte>(type: "tinyint", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequireLogin = table.Column<bool>(type: "bit", nullable: false),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    EndUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ApprovalStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                    table.CheckConstraint("CK_Vouchers_ApprovalStatus", "[ApprovalStatus] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_Vouchers_Discount", "[DiscountValue] > 0 AND ([DiscountType] <> 0 OR [DiscountValue] BETWEEN 1 AND 100) AND [DiscountType] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_Vouchers_Source", "[Source] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Vouchers_Usage", "[UsedCount] >= 0 AND [MaxUsagePerUser] >= 1 AND ([MaxUsageTotal] IS NULL OR [MaxUsageTotal] >= 1)");
                    table.CheckConstraint("CK_Vouchers_Window", "[StartUtc] IS NULL OR [EndUtc] IS NULL OR [EndUtc] > [StartUtc]");
                    table.ForeignKey(
                        name: "FK_Vouchers_Staff_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Vouchers_Staff_ReviewedByStaffId",
                        column: x => x.ReviewedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Vouchers_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserVouchers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoucherId = table.Column<int>(type: "int", nullable: false),
                    UsedCount = table.Column<int>(type: "int", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserVouchers", x => x.Id);
                    table.CheckConstraint("CK_UserVouchers_UsedCount", "[UsedCount] >= 0");
                    table.ForeignKey(
                        name: "FK_UserVouchers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserVouchers_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_VoucherId",
                table: "Orders",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_UserVouchers_UserId",
                table: "UserVouchers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserVouchers_VoucherId_UserId",
                table: "UserVouchers",
                columns: new[] { "VoucherId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_ApprovalStatus_CreatedAtUtc",
                table: "Vouchers",
                columns: new[] { "ApprovalStatus", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_BatchId",
                table: "Vouchers",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Code",
                table: "Vouchers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_CreatedByStaffId",
                table: "Vouchers",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_OwnerUserId",
                table: "Vouchers",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_ReviewedByStaffId",
                table: "Vouchers",
                column: "ReviewedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Vouchers_VoucherId",
                table: "Orders",
                column: "VoucherId",
                principalTable: "Vouchers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Vouchers_VoucherId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "UserVouchers");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Orders_VoucherId",
                table: "Orders");
        }
    }
}
