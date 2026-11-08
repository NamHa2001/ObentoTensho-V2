using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerAvatar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AvatarImageId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByStaffId",
                table: "Images",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Images",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_AvatarImageId",
                table: "Users",
                column: "AvatarImageId",
                filter: "[AvatarImageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Images_CreatedByUserId",
                table: "Images",
                column: "CreatedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Images_Uploader",
                table: "Images",
                sql: "([CreatedByStaffId] IS NOT NULL AND [CreatedByUserId] IS NULL) OR ([CreatedByStaffId] IS NULL AND [CreatedByUserId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Users_CreatedByUserId",
                table: "Images",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Images_AvatarImageId",
                table: "Users",
                column: "AvatarImageId",
                principalTable: "Images",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Images_Users_CreatedByUserId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Images_AvatarImageId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_AvatarImageId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Images_CreatedByUserId",
                table: "Images");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Images_Uploader",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "AvatarImageId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Images");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByStaffId",
                table: "Images",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
