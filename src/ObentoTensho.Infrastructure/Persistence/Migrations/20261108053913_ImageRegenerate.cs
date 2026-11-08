using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImageRegenerate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RegenerateRequested",
                table: "Images",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Images_Regenerate",
                table: "Images",
                column: "CreatedAtUtc",
                filter: "[RegenerateRequested] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Images_Regenerate",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "RegenerateRequested",
                table: "Images");
        }
    }
}
