using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHasDataSeeding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Transactions",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Funds",
                keyColumn: "Id",
                keyValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "DepartmentCode", "Name", "ParentId" },
                values: new object[] { 1, "DPW", "Public Works", null });

            migrationBuilder.InsertData(
                table: "Funds",
                columns: new[] { "Id", "FundCode", "Name", "Type" },
                values: new object[,]
                {
                    { 1, "100", "General Fund", 1 },
                    { 2, "200", "Utility Fund", 2 }
                });

            migrationBuilder.InsertData(
                table: "BudgetEntries",
                columns: new[] { "Id", "AccountNumber", "ActivityCode", "ActualAmount", "BudgetedAmount", "CreatedAt", "DepartmentId", "Description", "EncumbranceAmount", "EndPeriod", "FiscalYear", "FundId", "FundType", "IsGASBCompliant", "MunicipalAccountId", "ParentId", "SourceFilePath", "SourceRowNumber", "StartPeriod", "UpdatedAt", "Variance" },
                values: new object[] { 1, "405", "GOV", 0m, 50000m, new DateTime(2025, 10, 13, 15, 32, 11, 255, DateTimeKind.Utc).AddTicks(4849), 1, "Road Maintenance", 0m, new DateOnly(1, 1, 1), 2026, 1, 0, true, null, null, null, null, new DateOnly(1, 1, 1), null, 0m });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "DepartmentCode", "Name", "ParentId" },
                values: new object[] { 2, "SAN", "Sanitation", 1 });

            migrationBuilder.InsertData(
                table: "BudgetEntries",
                columns: new[] { "Id", "AccountNumber", "ActivityCode", "ActualAmount", "BudgetedAmount", "CreatedAt", "DepartmentId", "Description", "EncumbranceAmount", "EndPeriod", "FiscalYear", "FundId", "FundType", "IsGASBCompliant", "MunicipalAccountId", "ParentId", "SourceFilePath", "SourceRowNumber", "StartPeriod", "UpdatedAt", "Variance" },
                values: new object[] { 2, "405.1", "GOV", 0m, 20000m, new DateTime(2025, 10, 13, 15, 32, 11, 255, DateTimeKind.Utc).AddTicks(5648), 1, "Paving", 0m, new DateOnly(1, 1, 1), 2026, 1, 0, true, null, 1, null, null, new DateOnly(1, 1, 1), null, 0m });

            migrationBuilder.InsertData(
                table: "Transactions",
                columns: new[] { "Id", "Amount", "BudgetEntryId", "CreatedAt", "Description", "MunicipalAccountId", "TransactionDate", "Type", "UpdatedAt" },
                values: new object[] { 1, 10000m, 1, new DateTime(2025, 10, 13, 15, 32, 11, 255, DateTimeKind.Utc).AddTicks(6154), "Initial payment for road work", null, new DateTime(2025, 10, 13, 12, 0, 0, 0, DateTimeKind.Utc), "Payment", null });
        }
    }
}
