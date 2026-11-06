using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuthAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorType = table.Column<string>(type: "varchar(10)", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    EntityType = table.Column<string>(type: "varchar(50)", nullable: false),
                    EntityId = table.Column<string>(type: "varchar(100)", nullable: false),
                    OldDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(45)", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.CheckConstraint("CK_AuditLogs_ActorType", "[ActorType] IN ('staff', 'user', 'system')");
                });

            migrationBuilder.CreateTable(
                name: "Staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Email = table.Column<string>(type: "varchar(150)", nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(256)", nullable: true),
                    Role = table.Column<string>(type: "varchar(30)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    InviteTokenHash = table.Column<string>(type: "char(64)", nullable: true),
                    InviteExpiryUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "char(32)", nullable: false),
                    TotpSecretEncrypted = table.Column<byte[]>(type: "varbinary(256)", nullable: true),
                    TotpEnabled = table.Column<bool>(type: "bit", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEndUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    PasswordChangedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.Id);
                    table.CheckConstraint("CK_Staff_Email_Lowercase", "[Email] COLLATE Latin1_General_BIN2 = LOWER([Email]) COLLATE Latin1_General_BIN2");
                    table.CheckConstraint("CK_Staff_FailedLoginCount", "[FailedLoginCount] >= 0");
                    table.CheckConstraint("CK_Staff_Role", "[Role] IN ('SuperAdmin', 'Manager', 'Reception', 'Kitchen', 'Cashier', 'ContentEditor')");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Email = table.Column<string>(type: "varchar(150)", nullable: false),
                    EmailCanonical = table.Column<string>(type: "varchar(150)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "varchar(15)", nullable: true),
                    IsPhoneVerified = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(256)", nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    DateOfBirthSetAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DateOfBirthEditCount = table.Column<byte>(type: "tinyint", nullable: false),
                    PendingEmail = table.Column<string>(type: "varchar(150)", nullable: true),
                    PendingEmailTokenHash = table.Column<string>(type: "char(64)", nullable: true),
                    PendingEmailExpiryUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    MemberTier = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    CurrentPoints = table.Column<int>(type: "int", nullable: false),
                    PointsDebt = table.Column<int>(type: "int", nullable: false),
                    AccumulatedPoints = table.Column<int>(type: "int", nullable: false),
                    IsEmailVerified = table.Column<bool>(type: "bit", nullable: false),
                    EmailVerifyTokenHash = table.Column<string>(type: "char(64)", nullable: true),
                    EmailVerifyExpiryUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ResetPasswordTokenHash = table.Column<string>(type: "char(64)", nullable: true),
                    ResetPasswordExpiryUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEndUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    PasswordChangedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "char(32)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    PreferredLang = table.Column<string>(type: "char(2)", nullable: false, defaultValue: "vi"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_Email_Lowercase", "[Email] COLLATE Latin1_General_BIN2 = LOWER([Email]) COLLATE Latin1_General_BIN2");
                    table.CheckConstraint("CK_Users_FailedLoginCount", "[FailedLoginCount] >= 0");
                    table.CheckConstraint("CK_Users_MemberTier", "[MemberTier] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Users_Points", "[CurrentPoints] >= 0 AND [PointsDebt] >= 0 AND [AccumulatedPoints] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TokenHash = table.Column<string>(type: "char(64)", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ReplacedByTokenHash = table.Column<string>(type: "char(64)", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RevokedReason = table.Column<string>(type: "varchar(30)", nullable: true),
                    CreatedByIp = table.Column<string>(type: "varchar(45)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.CheckConstraint("CK_RefreshTokens_Owner", "(CASE WHEN [UserId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [StaffId] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.CheckConstraint("CK_RefreshTokens_RevokedReason", "[RevokedReason] IS NULL OR [RevokedReason] IN ('rotated', 'logout', 'reuse_detected', 'password_changed', 'account_locked')");
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorId_CreatedAtUtc",
                table: "AuditLogs",
                columns: new[] { "ActorId", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_CreatedAtUtc",
                table: "AuditLogs",
                columns: new[] { "EntityType", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ExpiresAtUtc",
                table: "RefreshTokens",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyId",
                table: "RefreshTokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_StaffId",
                table: "RefreshTokens",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Staff_Email",
                table: "Staff",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmailCanonical",
                table: "Users",
                column: "EmailCanonical");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneNumber",
                table: "Users",
                column: "PhoneNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Staff");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
