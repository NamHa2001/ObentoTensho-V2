using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReservationPendingAlert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PendingAlertedAtUtc",
                table: "Reservations",
                type: "datetime2(3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingAlertedAtUtc",
                table: "Reservations");
        }
    }
}
