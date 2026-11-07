using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DataExports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataExports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    RequestedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    DownloadTokenHash = table.Column<string>(type: "char(64)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataExports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataExports_Staff_RequestedByStaffId",
                        column: x => x.RequestedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataExports_DownloadTokenHash",
                table: "DataExports",
                column: "DownloadTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_DataExports_RequestedByStaffId_Kind_CreatedAtUtc",
                table: "DataExports",
                columns: new[] { "RequestedByStaffId", "Kind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DataExports_Status_CreatedAtUtc",
                table: "DataExports",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataExports");
        }
    }
}
