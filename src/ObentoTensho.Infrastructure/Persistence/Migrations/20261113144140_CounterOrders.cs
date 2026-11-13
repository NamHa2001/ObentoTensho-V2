using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CounterOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Channel",
                table: "Orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Channel",
                table: "Orders",
                sql: "[Channel] BETWEEN 0 AND 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Channel",
                table: "Orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Channel",
                table: "Orders",
                sql: "[Channel] BETWEEN 0 AND 2");
        }
    }
}
