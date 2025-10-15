using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUseSeedingForFY2026Budget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Enterprises",
                keyColumn: "Id",
                keyValue: 1,
                column: "BudgetAmount",
                value: 285755.00m);

            migrationBuilder.UpdateData(
                table: "Enterprises",
                keyColumn: "Id",
                keyValue: 2,
                column: "BudgetAmount",
                value: 5879527.00m);

            migrationBuilder.UpdateData(
                table: "Enterprises",
                keyColumn: "Id",
                keyValue: 3,
                column: "BudgetAmount",
                value: 285755.00m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Enterprises",
                keyColumn: "Id",
                keyValue: 1,
                column: "BudgetAmount",
                value: 2500000.00m);

            migrationBuilder.UpdateData(
                table: "Enterprises",
                keyColumn: "Id",
                keyValue: 2,
                column: "BudgetAmount",
                value: 1800000.00m);

            migrationBuilder.UpdateData(
                table: "Enterprises",
                keyColumn: "Id",
                keyValue: 3,
                column: "BudgetAmount",
                value: 3200000.00m);
        }
    }
}
