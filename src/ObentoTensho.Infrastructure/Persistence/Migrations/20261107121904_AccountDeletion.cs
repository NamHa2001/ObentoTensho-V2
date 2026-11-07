using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionScheduledAtUtc",
                table: "Users",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeletionScheduledAtUtc",
                table: "Users",
                column: "DeletionScheduledAtUtc",
                filter: "[DeletionScheduledAtUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_DeletionScheduledAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletionScheduledAtUtc",
                table: "Users");
        }
    }
}
