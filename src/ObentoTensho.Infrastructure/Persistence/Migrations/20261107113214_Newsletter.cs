using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Newsletter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsentRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Email = table.Column<string>(type: "varchar(150)", nullable: true),
                    DocType = table.Column<string>(type: "varchar(30)", nullable: false),
                    Version = table.Column<string>(type: "varchar(20)", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Ip = table.Column<string>(type: "varchar(45)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsentRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NewsletterCampaigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Subject = table.Column<string>(type: "nvarchar(300)", nullable: false),
                    ContentHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RecipientCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterCampaigns", x => x.Id);
                    table.CheckConstraint("CK_NewsletterCampaigns_Status", "[Status] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_NewsletterCampaigns_Staff_SentByStaffId",
                        column: x => x.SentByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NewsletterSubscribers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "varchar(150)", nullable: false),
                    Lang = table.Column<string>(type: "char(2)", nullable: false, defaultValue: "vi"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ConfirmTokenHash = table.Column<string>(type: "char(64)", nullable: true),
                    ConfirmExpiryUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UnsubscribeTokenHash = table.Column<string>(type: "char(64)", nullable: false),
                    UnsubscribeTokenProtected = table.Column<byte[]>(type: "varbinary(512)", nullable: false),
                    SubscribedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UnsubscribedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterSubscribers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsletterRecipients",
                columns: table => new
                {
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    SubscriberId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Attempts = table.Column<byte>(type: "tinyint", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(300)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterRecipients", x => new { x.CampaignId, x.SubscriberId });
                    table.CheckConstraint("CK_NewsletterRecipients_Status", "[Status] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_NewsletterRecipients_NewsletterCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "NewsletterCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NewsletterRecipients_NewsletterSubscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "NewsletterSubscribers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_Email_DocType",
                table: "ConsentRecords",
                columns: new[] { "Email", "DocType" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_UserId_DocType",
                table: "ConsentRecords",
                columns: new[] { "UserId", "DocType" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterCampaigns_SentByStaffId",
                table: "NewsletterCampaigns",
                column: "SentByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterCampaigns_Status_ScheduledAtUtc",
                table: "NewsletterCampaigns",
                columns: new[] { "Status", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterRecipients_CampaignId_Status",
                table: "NewsletterRecipients",
                columns: new[] { "CampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterRecipients_SubscriberId",
                table: "NewsletterRecipients",
                column: "SubscriberId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSubscribers_ConfirmTokenHash",
                table: "NewsletterSubscribers",
                column: "ConfirmTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSubscribers_Email",
                table: "NewsletterSubscribers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSubscribers_UnsubscribeTokenHash",
                table: "NewsletterSubscribers",
                column: "UnsubscribeTokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentRecords");

            migrationBuilder.DropTable(
                name: "NewsletterRecipients");

            migrationBuilder.DropTable(
                name: "NewsletterCampaigns");

            migrationBuilder.DropTable(
                name: "NewsletterSubscribers");
        }
    }
}
