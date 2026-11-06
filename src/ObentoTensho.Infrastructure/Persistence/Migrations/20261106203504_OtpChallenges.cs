using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OtpChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OtpChallenges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Purpose = table.Column<byte>(type: "tinyint", nullable: false),
                    TargetHash = table.Column<string>(type: "char(64)", nullable: false),
                    CodeHash = table.Column<string>(type: "char(64)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Attempts = table.Column<byte>(type: "tinyint", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IpHash = table.Column<string>(type: "char(64)", nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpChallenges", x => x.Id);
                    table.CheckConstraint("CK_OtpChallenges_Attempts", "[Attempts] >= 0");
                    table.CheckConstraint("CK_OtpChallenges_Purpose", "[Purpose] BETWEEN 0 AND 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_CreatedAtUtc",
                table: "OtpChallenges",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_IpHash_CreatedAtUtc",
                table: "OtpChallenges",
                columns: new[] { "IpHash", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_TargetHash_CreatedAtUtc",
                table: "OtpChallenges",
                columns: new[] { "TargetHash", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OtpChallenges");
        }
    }
}
