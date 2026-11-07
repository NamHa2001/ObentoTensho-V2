using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Loyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoyaltyAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RequestedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ReviewedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyAdjustments", x => x.Id);
                    table.CheckConstraint("CK_LoyaltyAdjustments_Points", "[Points] <> 0");
                    table.CheckConstraint("CK_LoyaltyAdjustments_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_LoyaltyAdjustments_Staff_RequestedByStaffId",
                        column: x => x.RequestedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LoyaltyAdjustments_Staff_ReviewedByStaffId",
                        column: x => x.ReviewedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LoyaltyAdjustments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyPoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<byte>(type: "tinyint", nullable: false),
                    ReferenceId = table.Column<string>(type: "varchar(100)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyPoints", x => x.Id);
                    table.CheckConstraint("CK_LoyaltyPoints_BalanceAfter", "[BalanceAfter] >= 0");
                    table.CheckConstraint("CK_LoyaltyPoints_Type", "[TransactionType] BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_LoyaltyPoints_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyLots",
                columns: table => new
                {
                    LedgerId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemainingPoints = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ExpiryNoticeSentAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyLots", x => x.LedgerId);
                    table.CheckConstraint("CK_LoyaltyLots_RemainingPoints", "[RemainingPoints] >= 0");
                    table.ForeignKey(
                        name: "FK_LoyaltyLots_LoyaltyPoints_LedgerId",
                        column: x => x.LedgerId,
                        principalTable: "LoyaltyPoints",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LoyaltyLots_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyAdjustments_RequestedByStaffId",
                table: "LoyaltyAdjustments",
                column: "RequestedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyAdjustments_ReviewedByStaffId",
                table: "LoyaltyAdjustments",
                column: "ReviewedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyAdjustments_Status_RequestedAtUtc",
                table: "LoyaltyAdjustments",
                columns: new[] { "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyAdjustments_UserId",
                table: "LoyaltyAdjustments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyLots_ExpiresAtUtc",
                table: "LoyaltyLots",
                column: "ExpiresAtUtc",
                filter: "[RemainingPoints] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyLots_UserId_ExpiresAtUtc",
                table: "LoyaltyLots",
                columns: new[] { "UserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPoints_UserId_Id",
                table: "LoyaltyPoints",
                columns: new[] { "UserId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPoints_UserId_TransactionType_ReferenceId",
                table: "LoyaltyPoints",
                columns: new[] { "UserId", "TransactionType", "ReferenceId" },
                unique: true,
                filter: "[ReferenceId] IS NOT NULL");

            // DB-10: sổ cái điểm chỉ thêm — tài khoản ứng dụng không sửa/xóa được
            migrationBuilder.Sql("DENY UPDATE, DELETE ON OBJECT::dbo.LoyaltyPoints TO [ot_app_runtime];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoyaltyAdjustments");

            migrationBuilder.DropTable(
                name: "LoyaltyLots");

            migrationBuilder.DropTable(
                name: "LoyaltyPoints");
        }
    }
}
