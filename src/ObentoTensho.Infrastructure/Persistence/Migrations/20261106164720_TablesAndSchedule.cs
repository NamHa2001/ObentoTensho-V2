using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TablesAndSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlockedDates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BlockedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AppliesTo = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    StartTime = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", nullable: true),
                    CreatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedDates", x => x.Id);
                    table.CheckConstraint("CK_BlockedDates_AppliesTo", "[AppliesTo] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_BlockedDates_Range", "([StartTime] IS NULL AND [EndTime] IS NULL) OR ([StartTime] IS NOT NULL AND [EndTime] IS NOT NULL AND [StartTime] < [EndTime])");
                    table.ForeignKey(
                        name: "FK_BlockedDates_Staff_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OpeningHours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayOfWeek = table.Column<byte>(type: "tinyint", nullable: false),
                    OpenTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    CloseTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    CrossesMidnight = table.Column<bool>(type: "bit", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningHours", x => x.Id);
                    table.CheckConstraint("CK_OpeningHours_DayOfWeek", "[DayOfWeek] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_OpeningHours_Range", "[IsClosed] = 1 OR ([CrossesMidnight] = 0 AND [OpenTime] < [CloseTime]) OR ([CrossesMidnight] = 1 AND [CloseTime] < [OpenTime])");
                });

            migrationBuilder.CreateTable(
                name: "ServiceSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayOfWeek = table.Column<byte>(type: "tinyint", nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    MaxReservations = table.Column<int>(type: "int", nullable: false),
                    MaxGuests = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceSlots", x => x.Id);
                    table.CheckConstraint("CK_ServiceSlots_Capacity", "[MaxReservations] >= 1 AND ([MaxGuests] IS NULL OR [MaxGuests] >= 1)");
                    table.CheckConstraint("CK_ServiceSlots_DayOfWeek", "[DayOfWeek] IS NULL OR [DayOfWeek] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_ServiceSlots_Range", "[StartTime] < [EndTime]");
                });

            migrationBuilder.CreateTable(
                name: "SpecialHours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    OpenTime = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    CloseTime = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", nullable: true),
                    CreatedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialHours", x => x.Id);
                    table.CheckConstraint("CK_SpecialHours_Range", "([IsClosed] = 1 AND [OpenTime] IS NULL AND [CloseTime] IS NULL) OR ([IsClosed] = 0 AND [OpenTime] < [CloseTime])");
                    table.ForeignKey(
                        name: "FK_SpecialHours_Staff_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TableAreas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableAreas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AreaId = table.Column<int>(type: "int", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    MinCapacity = table.Column<int>(type: "int", nullable: false),
                    MaxCapacity = table.Column<int>(type: "int", nullable: false),
                    CurrentStatus = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    PosX = table.Column<int>(type: "int", nullable: false),
                    PosY = table.Column<int>(type: "int", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    Rotation = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tables", x => x.Id);
                    table.CheckConstraint("CK_Tables_Capacity", "[MinCapacity] >= 1 AND [MaxCapacity] >= [MinCapacity]");
                    table.CheckConstraint("CK_Tables_CurrentStatus", "[CurrentStatus] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Tables_Layout", "[PosX] >= 0 AND [PosY] >= 0 AND [Width] >= 0 AND [Height] >= 0 AND [Rotation] BETWEEN 0 AND 359");
                    table.ForeignKey(
                        name: "FK_Tables_TableAreas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "TableAreas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockedDates_BlockedDate",
                table: "BlockedDates",
                column: "BlockedDate");

            migrationBuilder.CreateIndex(
                name: "IX_BlockedDates_CreatedByStaffId",
                table: "BlockedDates",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningHours_DayOfWeek_OpenTime",
                table: "OpeningHours",
                columns: new[] { "DayOfWeek", "OpenTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecialHours_CreatedByStaffId",
                table: "SpecialHours",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialHours_Date_OpenTime",
                table: "SpecialHours",
                columns: new[] { "Date", "OpenTime" },
                unique: true,
                filter: "[OpenTime] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TableAreas_Name",
                table: "TableAreas",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tables_AreaId_TableName",
                table: "Tables",
                columns: new[] { "AreaId", "TableName" },
                unique: true);

            // BR-TBL-12: đủ 7 ngày; mặc định "đóng cửa" cho tới khi SuperAdmin nhập giờ thật
            migrationBuilder.Sql(
                "INSERT INTO [OpeningHours] ([DayOfWeek], [OpenTime], [CloseTime], [CrossesMidnight], [IsClosed]) " +
                "VALUES (0, '00:00', '00:00', 0, 1), (1, '00:00', '00:00', 0, 1), (2, '00:00', '00:00', 0, 1), (3, '00:00', '00:00', 0, 1), " +
                "(4, '00:00', '00:00', 0, 1), (5, '00:00', '00:00', 0, 1), (6, '00:00', '00:00', 0, 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedDates");

            migrationBuilder.DropTable(
                name: "OpeningHours");

            migrationBuilder.DropTable(
                name: "ServiceSlots");

            migrationBuilder.DropTable(
                name: "SpecialHours");

            migrationBuilder.DropTable(
                name: "Tables");

            migrationBuilder.DropTable(
                name: "TableAreas");
        }
    }
}
