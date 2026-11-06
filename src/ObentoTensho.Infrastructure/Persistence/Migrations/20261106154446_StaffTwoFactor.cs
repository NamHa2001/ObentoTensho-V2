using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffTwoFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TotpLastUsedStep",
                table: "Staff",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StaffRecoveryCodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "char(64)", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffRecoveryCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffRecoveryCodes_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Staff_InviteTokenHash",
                table: "Staff",
                column: "InviteTokenHash",
                unique: true,
                filter: "[InviteTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StaffRecoveryCodes_StaffId_CodeHash",
                table: "StaffRecoveryCodes",
                columns: new[] { "StaffId", "CodeHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffRecoveryCodes");

            migrationBuilder.DropIndex(
                name: "IX_Staff_InviteTokenHash",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "TotpLastUsedStep",
                table: "Staff");
        }
    }
}
