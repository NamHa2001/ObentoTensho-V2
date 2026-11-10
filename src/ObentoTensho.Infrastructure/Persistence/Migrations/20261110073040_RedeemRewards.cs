using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RedeemRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RedeemRewards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PointsCost = table.Column<int>(type: "int", nullable: false),
                    DiscountType = table.Column<byte>(type: "tinyint", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxDiscountAmount = table.Column<decimal>(type: "decimal(18,0)", nullable: true),
                    MinOrderValue = table.Column<decimal>(type: "decimal(18,0)", nullable: false),
                    ValidDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedeemRewards", x => x.Id);
                    table.CheckConstraint("CK_RedeemRewards_Discount", "[DiscountValue] > 0 AND ([DiscountType] <> 0 OR [DiscountValue] BETWEEN 1 AND 100) AND [DiscountType] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_RedeemRewards_Points", "[PointsCost] >= 1");
                    table.CheckConstraint("CK_RedeemRewards_ValidDays", "[ValidDays] BETWEEN 1 AND 365");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RedeemRewards_IsActive_DisplayOrder",
                table: "RedeemRewards",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RedeemRewards_Name",
                table: "RedeemRewards",
                column: "Name",
                unique: true);

            // Giữ đúng cách đổi điểm cũ (100 điểm → voucher 30.000₫, hạn 30 ngày) làm phần thưởng đầu tiên
            migrationBuilder.Sql("""
                INSERT INTO [RedeemRewards] ([Name], [Description], [PointsCost], [DiscountType], [DiscountValue], [MaxDiscountAmount], [MinOrderValue], [ValidDays], [IsActive], [DisplayOrder], [CreatedAtUtc])
                VALUES (N'Voucher giảm 30.000₫', N'Dùng cho một đơn đặt món mang về bất kỳ.', 100, 1, 30000, NULL, 0, 30, 1, 1, SYSUTCDATETIME());
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RedeemRewards");
        }
    }
}
