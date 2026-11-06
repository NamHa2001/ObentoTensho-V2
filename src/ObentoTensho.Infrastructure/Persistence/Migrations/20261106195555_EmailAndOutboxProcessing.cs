using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmailAndOutboxProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Pending",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<DateTime>(
                name: "ResetPasswordExpiryUtc",
                table: "Staff",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResetPasswordTokenHash",
                table: "Staff",
                type: "char(64)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAtUtc",
                table: "OutboxMessages",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntilUtc",
                table: "OutboxMessages",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAtUtc",
                table: "OutboxMessages",
                type: "datetime2(3)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Tin có từ trước: tới hạn ngay từ lúc tạo
            migrationBuilder.Sql("UPDATE [OutboxMessages] SET [NextAttemptAtUtc] = [CreatedAtUtc];");

            migrationBuilder.CreateTable(
                name: "EmailLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "varchar(50)", nullable: false),
                    ToEmail = table.Column<string>(type: "varchar(150)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    RelatedEntityId = table.Column<string>(type: "varchar(100)", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "varchar(150)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailLogs", x => x.Id);
                    table.CheckConstraint("CK_EmailLogs_Attempts", "[Attempts] >= 0");
                    table.CheckConstraint("CK_EmailLogs_Status", "[Status] BETWEEN 0 AND 3");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmailVerifyTokenHash",
                table: "Users",
                column: "EmailVerifyTokenHash",
                unique: true,
                filter: "[EmailVerifyTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ResetPasswordTokenHash",
                table: "Users",
                column: "ResetPasswordTokenHash",
                unique: true,
                filter: "[ResetPasswordTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Staff_ResetPasswordTokenHash",
                table: "Staff",
                column: "ResetPasswordTokenHash",
                unique: true,
                filter: "[ResetPasswordTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                table: "OutboxMessages",
                columns: new[] { "NextAttemptAtUtc", "Id" },
                filter: "[ProcessedAtUtc] IS NULL AND [FailedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_IdempotencyKey",
                table: "EmailLogs",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_SentAtUtc",
                table: "EmailLogs",
                column: "SentAtUtc",
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_ToEmail_SentAtUtc",
                table: "EmailLogs",
                columns: new[] { "ToEmail", "SentAtUtc" },
                filter: "[Status] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailLogs");

            migrationBuilder.DropIndex(
                name: "IX_Users_EmailVerifyTokenHash",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ResetPasswordTokenHash",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Staff_ResetPasswordTokenHash",
                table: "Staff");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Pending",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "ResetPasswordExpiryUtc",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "ResetPasswordTokenHash",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "FailedAtUtc",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LockedUntilUtc",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                table: "OutboxMessages",
                column: "Id",
                filter: "[ProcessedAtUtc] IS NULL");
        }
    }
}
