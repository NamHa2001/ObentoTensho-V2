using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContentMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalPath = table.Column<string>(type: "nvarchar(300)", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    BlurHash = table.Column<string>(type: "varchar(40)", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    VariantWidths = table.Column<string>(type: "varchar(60)", nullable: true),
                    Format = table.Column<string>(type: "varchar(10)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    AltText = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    ContentHash = table.Column<string>(type: "char(64)", nullable: false),
                    CreatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.Id);
                    table.CheckConstraint("CK_Images_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_Images_Staff_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Banners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageMobileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    Subtitle = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    CtaText = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    CtaLink = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ShowFromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ShowToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banners", x => x.Id);
                    table.CheckConstraint("CK_Banners_Dates", "[ShowFromDate] IS NULL OR [ShowToDate] IS NULL OR [ShowToDate] >= [ShowFromDate]");
                    table.ForeignKey(
                        name: "FK_Banners_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Banners_Images_ImageMobileId",
                        column: x => x.ImageMobileId,
                        principalTable: "Images",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Gallery",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MediaType = table.Column<string>(type: "varchar(10)", nullable: false),
                    ImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VideoUrl = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    Category = table.Column<string>(type: "varchar(20)", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gallery", x => x.Id);
                    table.CheckConstraint("CK_Gallery_Category", "[Category] IN ('interior', 'exterior', 'private', 'food', 'event')");
                    table.CheckConstraint("CK_Gallery_Media", "([MediaType] = 'image' AND [ImageId] IS NOT NULL) OR ([MediaType] = 'video' AND [VideoUrl] IS NOT NULL)");
                    table.CheckConstraint("CK_Gallery_MediaType", "[MediaType] IN ('image', 'video')");
                    table.ForeignKey(
                        name: "FK_Gallery_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Banners_ImageId",
                table: "Banners",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Banners_ImageMobileId",
                table: "Banners",
                column: "ImageMobileId");

            migrationBuilder.CreateIndex(
                name: "IX_Banners_IsActive_DisplayOrder",
                table: "Banners",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Gallery_ImageId",
                table: "Gallery",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Gallery_IsActive_Category_DisplayOrder",
                table: "Gallery",
                columns: new[] { "IsActive", "Category", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Images_ContentHash",
                table: "Images",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_Images_CreatedByStaffId",
                table: "Images",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_Status",
                table: "Images",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Banners");

            migrationBuilder.DropTable(
                name: "Gallery");

            migrationBuilder.DropTable(
                name: "Images");
        }
    }
}
