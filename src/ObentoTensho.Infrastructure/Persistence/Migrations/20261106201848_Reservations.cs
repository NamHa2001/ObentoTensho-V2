using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Reservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyCounters",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "varchar(20)", nullable: false),
                    LastValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCounters", x => new { x.Date, x.Kind });
                    table.CheckConstraint("CK_DailyCounters_LastValue", "[LastValue] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationCode = table.Column<string>(type: "varchar(20)", nullable: false),
                    DailySeq = table.Column<int>(type: "int", nullable: false),
                    AccessTokenHash = table.Column<string>(type: "char(64)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GuestName = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    GuestPhone = table.Column<string>(type: "varchar(15)", nullable: false),
                    GuestEmail = table.Column<string>(type: "varchar(150)", nullable: true),
                    OtpRequired = table.Column<bool>(type: "bit", nullable: false),
                    ContactVerifiedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ContactConfirmedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ConfirmedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfirmNote = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    CallAttempts = table.Column<byte>(type: "tinyint", nullable: false),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Lang = table.Column<string>(type: "char(2)", nullable: false),
                    ReservedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReservedTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    ReservedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    SlotId = table.Column<int>(type: "int", nullable: false),
                    AdultCount = table.Column<int>(type: "int", nullable: false),
                    ChildCount = table.Column<int>(type: "int", nullable: false),
                    TableId = table.Column<int>(type: "int", nullable: true),
                    SpecialRequest = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    InternalNote = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CancelReason = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancelledByType = table.Column<string>(type: "varchar(10)", nullable: true),
                    CancelledById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReminderSentAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    PointsEarned = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.CheckConstraint("CK_Reservations_CancelledByType", "[CancelledByType] IS NULL OR [CancelledByType] IN ('guest', 'staff', 'system')");
                    table.CheckConstraint("CK_Reservations_Guests", "[AdultCount] >= 1 AND [ChildCount] >= 0");
                    table.CheckConstraint("CK_Reservations_Source", "[Source] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_Reservations_Status", "[Status] BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_Reservations_ServiceSlots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "ServiceSlots",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reservations_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Reservations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SlotDayCapacity",
                columns: table => new
                {
                    SlotId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    BookedCount = table.Column<int>(type: "int", nullable: false),
                    BookedGuests = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlotDayCapacity", x => new { x.SlotId, x.Date });
                    table.CheckConstraint("CK_SlotDayCapacity_Counts", "[BookedCount] >= 0 AND [BookedGuests] >= 0");
                    table.ForeignKey(
                        name: "FK_SlotDayCapacity_ServiceSlots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "ServiceSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReservationStatusHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    ToStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    ChangedByType = table.Column<string>(type: "varchar(10)", nullable: false),
                    ChangedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationStatusHistory", x => x.Id);
                    table.CheckConstraint("CK_ReservationStatusHistory_ChangedByType", "[ChangedByType] IN ('guest', 'staff', 'system')");
                    table.ForeignKey(
                        name: "FK_ReservationStatusHistory_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_GuestPhone_Status_ReservedDate",
                table: "Reservations",
                columns: new[] { "GuestPhone", "Status", "ReservedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ReservationCode",
                table: "Reservations",
                column: "ReservationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ReservedDate_Status",
                table: "Reservations",
                columns: new[] { "ReservedDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_SlotId",
                table: "Reservations",
                column: "SlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_TableId_ReservedDate",
                table: "Reservations",
                columns: new[] { "TableId", "ReservedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_UserId",
                table: "Reservations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationStatusHistory_ReservationId",
                table: "ReservationStatusHistory",
                column: "ReservationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyCounters");

            migrationBuilder.DropTable(
                name: "ReservationStatusHistory");

            migrationBuilder.DropTable(
                name: "SlotDayCapacity");

            migrationBuilder.DropTable(
                name: "Reservations");
        }
    }
}
