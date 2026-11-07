using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Menu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MenuCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Slug = table.Column<string>(type: "varchar(150)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OptionGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    SelectionType = table.Column<byte>(type: "tinyint", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    MinSelect = table.Column<byte>(type: "tinyint", nullable: false),
                    MaxSelect = table.Column<byte>(type: "tinyint", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionGroups", x => x.Id);
                    table.CheckConstraint("CK_OptionGroups_Selection", "[MinSelect] <= [MaxSelect] AND [MaxSelect] >= 1 AND ([SelectionType] = 1 OR [MaxSelect] = 1) AND ([IsRequired] = 0 OR [MinSelect] >= 1)");
                    table.CheckConstraint("CK_OptionGroups_SelectionType", "[SelectionType] BETWEEN 0 AND 1");
                });

            migrationBuilder.CreateTable(
                name: "MenuCategoryTranslations",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Lang = table.Column<string>(type: "char(2)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    SourceHash = table.Column<string>(type: "char(64)", nullable: false),
                    ReviewStatus = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    TranslatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuCategoryTranslations", x => new { x.CategoryId, x.Lang });
                    table.CheckConstraint("CK_MenuCategoryTranslations_Lang", "[Lang] IN ('en', 'ja')");
                    table.CheckConstraint("CK_MenuCategoryTranslations_ReviewStatus", "[ReviewStatus] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_MenuCategoryTranslations_MenuCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "MenuCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    NameVi = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    Slug = table.Column<string>(type: "varchar(200)", nullable: false),
                    NameSearch = table.Column<string>(type: "nvarchar(300)", nullable: false),
                    ShortDesc = table.Column<string>(type: "nvarchar(200)", nullable: true),
                    FullDescHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,0)", nullable: false),
                    OriginalPrice = table.Column<decimal>(type: "decimal(18,0)", nullable: true),
                    IsNew = table.Column<bool>(type: "bit", nullable: false),
                    IsBestseller = table.Column<bool>(type: "bit", nullable: false),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    IsVegetarian = table.Column<bool>(type: "bit", nullable: false),
                    IsSpicy = table.Column<bool>(type: "bit", nullable: false),
                    AllowTakeaway = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    SoldOutUntilUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Allergens = table.Column<string>(type: "varchar(100)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                    table.CheckConstraint("CK_MenuItems_Price", "[Price] > 0 AND ([OriginalPrice] IS NULL OR [OriginalPrice] > [Price])");
                    table.CheckConstraint("CK_MenuItems_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_MenuItems_MenuCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "MenuCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OptionGroupTranslations",
                columns: table => new
                {
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    Lang = table.Column<string>(type: "char(2)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    SourceHash = table.Column<string>(type: "char(64)", nullable: false),
                    ReviewStatus = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    TranslatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionGroupTranslations", x => new { x.GroupId, x.Lang });
                    table.CheckConstraint("CK_OptionGroupTranslations_Lang", "[Lang] IN ('en', 'ja')");
                    table.CheckConstraint("CK_OptionGroupTranslations_ReviewStatus", "[ReviewStatus] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_OptionGroupTranslations_OptionGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OptionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Options",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    PriceDelta = table.Column<decimal>(type: "decimal(18,0)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Allergens = table.Column<string>(type: "varchar(100)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Options", x => x.Id);
                    table.CheckConstraint("CK_Options_PriceDelta", "[PriceDelta] >= 0 AND [PriceDelta] <= 10000000");
                    table.CheckConstraint("CK_Options_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_Options_OptionGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OptionGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MenuItemOptionGroups",
                columns: table => new
                {
                    MenuItemId = table.Column<int>(type: "int", nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemOptionGroups", x => new { x.MenuItemId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_MenuItemOptionGroups_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemOptionGroups_OptionGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OptionGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MenuItemTranslations",
                columns: table => new
                {
                    MenuItemId = table.Column<int>(type: "int", nullable: false),
                    Lang = table.Column<string>(type: "char(2)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    ShortDesc = table.Column<string>(type: "nvarchar(200)", nullable: true),
                    FullDescHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceHash = table.Column<string>(type: "char(64)", nullable: false),
                    ReviewStatus = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    TranslatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemTranslations", x => new { x.MenuItemId, x.Lang });
                    table.CheckConstraint("CK_MenuItemTranslations_Lang", "[Lang] IN ('en', 'ja')");
                    table.CheckConstraint("CK_MenuItemTranslations_ReviewStatus", "[ReviewStatus] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_MenuItemTranslations_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OptionTranslations",
                columns: table => new
                {
                    OptionId = table.Column<int>(type: "int", nullable: false),
                    Lang = table.Column<string>(type: "char(2)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    SourceHash = table.Column<string>(type: "char(64)", nullable: false),
                    ReviewStatus = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    TranslatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionTranslations", x => new { x.OptionId, x.Lang });
                    table.CheckConstraint("CK_OptionTranslations_Lang", "[Lang] IN ('en', 'ja')");
                    table.CheckConstraint("CK_OptionTranslations_ReviewStatus", "[ReviewStatus] BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_OptionTranslations_Options_OptionId",
                        column: x => x.OptionId,
                        principalTable: "Options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_Slug",
                table: "MenuCategories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemOptionGroups_GroupId",
                table: "MenuItemOptionGroups",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_CategoryId_Status_DisplayOrder",
                table: "MenuItems",
                columns: new[] { "CategoryId", "Status", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_NameSearch",
                table: "MenuItems",
                column: "NameSearch");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_Slug",
                table: "MenuItems",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Options_GroupId",
                table: "Options",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuCategoryTranslations");

            migrationBuilder.DropTable(
                name: "MenuItemOptionGroups");

            migrationBuilder.DropTable(
                name: "MenuItemTranslations");

            migrationBuilder.DropTable(
                name: "OptionGroupTranslations");

            migrationBuilder.DropTable(
                name: "OptionTranslations");

            migrationBuilder.DropTable(
                name: "MenuItems");

            migrationBuilder.DropTable(
                name: "Options");

            migrationBuilder.DropTable(
                name: "MenuCategories");

            migrationBuilder.DropTable(
                name: "OptionGroups");
        }
    }
}
