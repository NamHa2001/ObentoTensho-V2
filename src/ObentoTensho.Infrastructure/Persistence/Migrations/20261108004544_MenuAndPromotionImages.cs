using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MenuAndPromotionImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BannerImageId",
                table: "Promotions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MainImageId",
                table: "MenuItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImageId",
                table: "MenuCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MenuItemImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MenuItemId = table.Column<int>(type: "int", nullable: false),
                    ImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemImages_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MenuItemImages_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_BannerImageId",
                table: "Promotions",
                column: "BannerImageId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_MainImageId",
                table: "MenuItems",
                column: "MainImageId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_ImageId",
                table: "MenuCategories",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemImages_ImageId",
                table: "MenuItemImages",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemImages_MenuItemId_ImageId",
                table: "MenuItemImages",
                columns: new[] { "MenuItemId", "ImageId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuCategories_Images_ImageId",
                table: "MenuCategories",
                column: "ImageId",
                principalTable: "Images",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_Images_MainImageId",
                table: "MenuItems",
                column: "MainImageId",
                principalTable: "Images",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Promotions_Images_BannerImageId",
                table: "Promotions",
                column: "BannerImageId",
                principalTable: "Images",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuCategories_Images_ImageId",
                table: "MenuCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_Images_MainImageId",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Promotions_Images_BannerImageId",
                table: "Promotions");

            migrationBuilder.DropTable(
                name: "MenuItemImages");

            migrationBuilder.DropIndex(
                name: "IX_Promotions_BannerImageId",
                table: "Promotions");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_MainImageId",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuCategories_ImageId",
                table: "MenuCategories");

            migrationBuilder.DropColumn(
                name: "BannerImageId",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "MainImageId",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "MenuCategories");
        }
    }
}
