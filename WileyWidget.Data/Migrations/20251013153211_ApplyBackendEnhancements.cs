using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApplyBackendEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 13, 15, 32, 11, 255, DateTimeKind.Utc).AddTicks(4849));

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 13, 15, 32, 11, 255, DateTimeKind.Utc).AddTicks(5648));

            migrationBuilder.UpdateData(
                table: "Transactions",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "TransactionDate" },
                values: new object[] { new DateTime(2025, 10, 13, 15, 32, 11, 255, DateTimeKind.Utc).AddTicks(6154), new DateTime(2025, 10, 13, 12, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 13, 15, 21, 30, 809, DateTimeKind.Utc).AddTicks(1044));

            migrationBuilder.UpdateData(
                table: "BudgetEntries",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 13, 15, 21, 30, 809, DateTimeKind.Utc).AddTicks(2333));

            migrationBuilder.UpdateData(
                table: "Transactions",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "TransactionDate" },
                values: new object[] { new DateTime(2025, 10, 13, 15, 21, 30, 809, DateTimeKind.Utc).AddTicks(3448), new DateTime(2025, 10, 13, 15, 21, 30, 809, DateTimeKind.Utc).AddTicks(4078) });
        }
    }
}
