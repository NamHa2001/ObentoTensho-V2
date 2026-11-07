using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReviewsContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContactMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Email = table.Column<string>(type: "varchar(150)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "varchar(20)", nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(45)", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(200)", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(2000)", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ReadByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RepliedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RepliedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactMessages_Staff_ReadByStaffId",
                        column: x => x.ReadByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContactMessages_Staff_RepliedByStaffId",
                        column: x => x.RepliedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuItemId = table.Column<int>(type: "int", nullable: true),
                    Rating = table.Column<byte>(type: "tinyint", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    ReplyContent = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    RepliedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RepliedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ReviewedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.CheckConstraint("CK_Reviews_Rating", "[Rating] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Reviews_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_Reviews_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Reviews_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_Staff_RepliedByStaffId",
                        column: x => x.RepliedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_Staff_ReviewedByStaffId",
                        column: x => x.ReviewedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContactMessages_Email_CreatedAtUtc",
                table: "ContactMessages",
                columns: new[] { "Email", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactMessages_IsRead_Id",
                table: "ContactMessages",
                columns: new[] { "IsRead", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactMessages_ReadByStaffId",
                table: "ContactMessages",
                column: "ReadByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactMessages_RepliedByStaffId",
                table: "ContactMessages",
                column: "RepliedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_MenuItemId",
                table: "Reviews",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_OrderId",
                table: "Reviews",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_RepliedByStaffId",
                table: "Reviews",
                column: "RepliedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewedByStaffId",
                table: "Reviews",
                column: "ReviewedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Status_MenuItemId_Id",
                table: "Reviews",
                columns: new[] { "Status", "MenuItemId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId_CreatedAtUtc",
                table: "Reviews",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId_OrderId_MenuItemId",
                table: "Reviews",
                columns: new[] { "UserId", "OrderId", "MenuItemId" },
                unique: true,
                filter: "[MenuItemId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactMessages");

            migrationBuilder.DropTable(
                name: "Reviews");
        }
    }
}
