using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContentTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BannerTranslations",
                columns: table => new
                {
                    BannerId = table.Column<int>(type: "int", nullable: false),
                    Lang = table.Column<string>(type: "char(2)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    CtaText = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    SourceHash = table.Column<string>(type: "char(64)", nullable: false),
                    ReviewStatus = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    TranslatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BannerTranslations", x => new { x.BannerId, x.Lang });
                    table.CheckConstraint("CK_BannerTranslations_Lang", "[Lang] IN ('en', 'ja')");
                    table.CheckConstraint("CK_BannerTranslations_ReviewStatus", "[ReviewStatus] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_BannerTranslations_Banners_BannerId",
                        column: x => x.BannerId,
                        principalTable: "Banners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GalleryTranslations",
                columns: table => new
                {
                    GalleryItemId = table.Column<int>(type: "int", nullable: false),
                    Lang = table.Column<string>(type: "char(2)", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(300)", nullable: false),
                    SourceHash = table.Column<string>(type: "char(64)", nullable: false),
                    ReviewStatus = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    TranslatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GalleryTranslations", x => new { x.GalleryItemId, x.Lang });
                    table.CheckConstraint("CK_GalleryTranslations_Lang", "[Lang] IN ('en', 'ja')");
                    table.CheckConstraint("CK_GalleryTranslations_ReviewStatus", "[ReviewStatus] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_GalleryTranslations_Gallery_GalleryItemId",
                        column: x => x.GalleryItemId,
                        principalTable: "Gallery",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NewsTranslations",
                columns: table => new
                {
                    NewsId = table.Column<int>(type: "int", nullable: false),
                    Lang = table.Column<string>(type: "char(2)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    BodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetaTitle = table.Column<string>(type: "nvarchar(200)", nullable: true),
                    MetaDescription = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    SourceHash = table.Column<string>(type: "char(64)", nullable: false),
                    ReviewStatus = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    TranslatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsTranslations", x => new { x.NewsId, x.Lang });
                    table.CheckConstraint("CK_NewsTranslations_Lang", "[Lang] IN ('en', 'ja')");
                    table.CheckConstraint("CK_NewsTranslations_ReviewStatus", "[ReviewStatus] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_NewsTranslations_News_NewsId",
                        column: x => x.NewsId,
                        principalTable: "News",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromotionTranslations",
                columns: table => new
                {
                    PromotionId = table.Column<int>(type: "int", nullable: false),
                    Lang = table.Column<string>(type: "char(2)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    BodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceHash = table.Column<string>(type: "char(64)", nullable: false),
                    ReviewStatus = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    TranslatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionTranslations", x => new { x.PromotionId, x.Lang });
                    table.CheckConstraint("CK_PromotionTranslations_Lang", "[Lang] IN ('en', 'ja')");
                    table.CheckConstraint("CK_PromotionTranslations_ReviewStatus", "[ReviewStatus] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_PromotionTranslations_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "Promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaticPageTranslations",
                columns: table => new
                {
                    StaticPageId = table.Column<int>(type: "int", nullable: false),
                    Lang = table.Column<string>(type: "char(2)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    BodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceHash = table.Column<string>(type: "char(64)", nullable: false),
                    ReviewStatus = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    TranslatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaticPageTranslations", x => new { x.StaticPageId, x.Lang });
                    table.CheckConstraint("CK_StaticPageTranslations_Lang", "[Lang] IN ('en', 'ja')");
                    table.CheckConstraint("CK_StaticPageTranslations_ReviewStatus", "[ReviewStatus] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_StaticPageTranslations_StaticPages_StaticPageId",
                        column: x => x.StaticPageId,
                        principalTable: "StaticPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BannerTranslations");

            migrationBuilder.DropTable(
                name: "GalleryTranslations");

            migrationBuilder.DropTable(
                name: "NewsTranslations");

            migrationBuilder.DropTable(
                name: "PromotionTranslations");

            migrationBuilder.DropTable(
                name: "StaticPageTranslations");
        }
    }
}
