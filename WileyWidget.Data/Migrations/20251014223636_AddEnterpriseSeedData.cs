using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnterpriseSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Enterprises",
                columns: new[] { "Id", "BudgetAmount", "CitizenCount", "CreatedBy", "CreatedDate", "CurrentRate", "DeletedBy", "DeletedDate", "Description", "IsDeleted", "LastModified", "MeterReadDate", "MeterReading", "ModifiedBy", "ModifiedDate", "MonthlyExpenses", "Name", "Notes", "PreviousMeterReadDate", "PreviousMeterReading", "Status", "TotalBudget", "Type" },
                values: new object[,]
                {
                    { 1, 2500000.00m, 12500, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 45.50m, null, null, null, false, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m, "Town of Wiley Water Department", null, null, null, 0, 0m, "Water" },
                    { 2, 1800000.00m, 12500, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 125.75m, null, null, null, false, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m, "Town of Wiley Sewer Department", null, null, null, 0, 0m, "Sewer" },
                    { 3, 3200000.00m, 12500, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.12m, null, null, null, false, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m, "Town of Wiley Electric Department", null, null, null, 0, 0m, "Electric" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Enterprises",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Enterprises",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Enterprises",
                keyColumn: "Id",
                keyValue: 3);
        }
    }
}
