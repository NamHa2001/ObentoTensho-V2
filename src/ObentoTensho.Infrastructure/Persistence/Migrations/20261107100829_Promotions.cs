using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Promotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BirthdayYear",
                table: "Vouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Promotions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    Slug = table.Column<string>(type: "varchar(200)", nullable: false),
                    DescriptionHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PublicTeaser = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.Id);
                    table.CheckConstraint("CK_Promotions_Dates", "[EndDate] >= [StartDate]");
                    table.ForeignKey(
                        name: "FK_Promotions_Staff_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_OwnerUserId_BirthdayYear",
                table: "Vouchers",
                columns: new[] { "OwnerUserId", "BirthdayYear" },
                unique: true,
                filter: "[BirthdayYear] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_PromotionId",
                table: "Vouchers",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_CreatedByStaffId",
                table: "Promotions",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_IsActive_EndDate",
                table: "Promotions",
                columns: new[] { "IsActive", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_Slug",
                table: "Promotions",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_Promotions_PromotionId",
                table: "Vouchers",
                column: "PromotionId",
                principalTable: "Promotions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_Promotions_PromotionId",
                table: "Vouchers");

            migrationBuilder.DropTable(
                name: "Promotions");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_OwnerUserId_BirthdayYear",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_PromotionId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "BirthdayYear",
                table: "Vouchers");
        }
    }
}
