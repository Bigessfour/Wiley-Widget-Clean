using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDataSeeding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_BudgetEntries_ParentId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_Departments_DepartmentId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_Funds_FundId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_EnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_SecondaryEnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoice_MunicipalAccounts_MunicipalAccountId",
                table: "Invoice");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoice_Vendor_VendorId",
                table: "Invoice");

            migrationBuilder.DropForeignKey(
                name: "FK_MunicipalAccounts_BudgetPeriod_BudgetPeriodId",
                table: "MunicipalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_MunicipalAccounts_Departments_DepartmentId",
                table: "MunicipalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_MunicipalAccounts_MunicipalAccounts_ParentAccountId",
                table: "MunicipalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_BudgetEntries_BudgetEntryId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                table: "Transactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Invoice",
                table: "Invoice");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BudgetPeriod",
                table: "BudgetPeriod");

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

            migrationBuilder.RenameTable(
                name: "Invoice",
                newName: "Invoices");

            migrationBuilder.RenameTable(
                name: "BudgetPeriod",
                newName: "BudgetPeriods");

            migrationBuilder.RenameColumn(
                name: "AccountNumber_Value",
                table: "MunicipalAccounts",
                newName: "AccountNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Invoice_VendorId",
                table: "Invoices",
                newName: "IX_Invoices_VendorId");

            migrationBuilder.RenameIndex(
                name: "IX_Invoice_MunicipalAccountId",
                table: "Invoices",
                newName: "IX_Invoices_MunicipalAccountId");

            migrationBuilder.AlterColumn<int>(
                name: "FundClass",
                table: "MunicipalAccounts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                table: "MunicipalAccounts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Enterprises",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Enterprises",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeChartsInReports",
                table: "AppSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReportEndDate",
                table: "AppSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReportStartDate",
                table: "AppSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastSelectedEnterpriseId",
                table: "AppSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastSelectedFormat",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSelectedReportType",
                table: "AppSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Invoices",
                table: "Invoices",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BudgetPeriods",
                table: "BudgetPeriods",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MunicipalAccounts_Fund_Type",
                table: "MunicipalAccounts",
                columns: new[] { "Fund", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceDate",
                table: "Invoices",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_IsActive",
                table: "BudgetPeriods",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_Year",
                table: "BudgetPeriods",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_Year_Status",
                table: "BudgetPeriods",
                columns: new[] { "Year", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_BudgetEntries_ParentId",
                table: "BudgetEntries",
                column: "ParentId",
                principalTable: "BudgetEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_Departments_DepartmentId",
                table: "BudgetEntries",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_Funds_FundId",
                table: "BudgetEntries",
                column: "FundId",
                principalTable: "Funds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId",
                table: "BudgetEntries",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_EnterpriseId",
                table: "BudgetInteraction",
                column: "EnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId",
                table: "BudgetInteraction",
                column: "PrimaryEnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_SecondaryEnterpriseId",
                table: "BudgetInteraction",
                column: "SecondaryEnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_MunicipalAccounts_MunicipalAccountId",
                table: "Invoices",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Vendor_VendorId",
                table: "Invoices",
                column: "VendorId",
                principalTable: "Vendor",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MunicipalAccounts_BudgetPeriods_BudgetPeriodId",
                table: "MunicipalAccounts",
                column: "BudgetPeriodId",
                principalTable: "BudgetPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MunicipalAccounts_Departments_DepartmentId",
                table: "MunicipalAccounts",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MunicipalAccounts_MunicipalAccounts_ParentAccountId",
                table: "MunicipalAccounts",
                column: "ParentAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_BudgetEntries_BudgetEntryId",
                table: "Transactions",
                column: "BudgetEntryId",
                principalTable: "BudgetEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                table: "Transactions",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_BudgetEntries_ParentId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_Departments_DepartmentId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_Funds_FundId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId",
                table: "BudgetEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_EnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_SecondaryEnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_MunicipalAccounts_MunicipalAccountId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Vendor_VendorId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_MunicipalAccounts_BudgetPeriods_BudgetPeriodId",
                table: "MunicipalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_MunicipalAccounts_Departments_DepartmentId",
                table: "MunicipalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_MunicipalAccounts_MunicipalAccounts_ParentAccountId",
                table: "MunicipalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_BudgetEntries_BudgetEntryId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_MunicipalAccounts_Fund_Type",
                table: "MunicipalAccounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Invoices",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceDate",
                table: "Invoices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BudgetPeriods",
                table: "BudgetPeriods");

            migrationBuilder.DropIndex(
                name: "IX_BudgetPeriods_IsActive",
                table: "BudgetPeriods");

            migrationBuilder.DropIndex(
                name: "IX_BudgetPeriods_Year",
                table: "BudgetPeriods");

            migrationBuilder.DropIndex(
                name: "IX_BudgetPeriods_Year_Status",
                table: "BudgetPeriods");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Enterprises");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Enterprises");

            migrationBuilder.DropColumn(
                name: "IncludeChartsInReports",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LastReportEndDate",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LastReportStartDate",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LastSelectedEnterpriseId",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LastSelectedFormat",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LastSelectedReportType",
                table: "AppSettings");

            migrationBuilder.RenameTable(
                name: "Invoices",
                newName: "Invoice");

            migrationBuilder.RenameTable(
                name: "BudgetPeriods",
                newName: "BudgetPeriod");

            migrationBuilder.RenameColumn(
                name: "AccountNumber",
                table: "MunicipalAccounts",
                newName: "AccountNumber_Value");

            migrationBuilder.RenameIndex(
                name: "IX_Invoices_VendorId",
                table: "Invoice",
                newName: "IX_Invoice_VendorId");

            migrationBuilder.RenameIndex(
                name: "IX_Invoices_MunicipalAccountId",
                table: "Invoice",
                newName: "IX_Invoice_MunicipalAccountId");

            migrationBuilder.AlterColumn<int>(
                name: "FundClass",
                table: "MunicipalAccounts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber_Value",
                table: "MunicipalAccounts",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Invoice",
                table: "Invoice",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BudgetPeriod",
                table: "BudgetPeriod",
                column: "Id");

            migrationBuilder.InsertData(
                table: "Enterprises",
                columns: new[] { "Id", "BudgetAmount", "CitizenCount", "CreatedBy", "CreatedDate", "CurrentRate", "DeletedBy", "DeletedDate", "Description", "IsDeleted", "LastModified", "MeterReadDate", "MeterReading", "ModifiedBy", "ModifiedDate", "MonthlyExpenses", "Name", "Notes", "PreviousMeterReadDate", "PreviousMeterReading", "Status", "TotalBudget", "Type" },
                values: new object[,]
                {
                    { 1, 285755.00m, 12500, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 45.50m, null, null, null, false, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m, "Town of Wiley Water Department", null, null, null, 0, 0m, "Water" },
                    { 2, 5879527.00m, 12500, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 125.75m, null, null, null, false, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m, "Town of Wiley Sewer Department", null, null, null, 0, 0m, "Sewer" },
                    { 3, 285755.00m, 12500, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.12m, null, null, null, false, null, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m, "Town of Wiley Electric Department", null, null, null, 0, 0m, "Electric" }
                });

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_BudgetEntries_ParentId",
                table: "BudgetEntries",
                column: "ParentId",
                principalTable: "BudgetEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_Departments_DepartmentId",
                table: "BudgetEntries",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_Funds_FundId",
                table: "BudgetEntries",
                column: "FundId",
                principalTable: "Funds",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetEntries_MunicipalAccounts_MunicipalAccountId",
                table: "BudgetEntries",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_EnterpriseId",
                table: "BudgetInteraction",
                column: "EnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_PrimaryEnterpriseId",
                table: "BudgetInteraction",
                column: "PrimaryEnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_SecondaryEnterpriseId",
                table: "BudgetInteraction",
                column: "SecondaryEnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoice_MunicipalAccounts_MunicipalAccountId",
                table: "Invoice",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoice_Vendor_VendorId",
                table: "Invoice",
                column: "VendorId",
                principalTable: "Vendor",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MunicipalAccounts_BudgetPeriod_BudgetPeriodId",
                table: "MunicipalAccounts",
                column: "BudgetPeriodId",
                principalTable: "BudgetPeriod",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MunicipalAccounts_Departments_DepartmentId",
                table: "MunicipalAccounts",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MunicipalAccounts_MunicipalAccounts_ParentAccountId",
                table: "MunicipalAccounts",
                column: "ParentAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_BudgetEntries_BudgetEntryId",
                table: "Transactions",
                column: "BudgetEntryId",
                principalTable: "BudgetEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_MunicipalAccounts_MunicipalAccountId",
                table: "Transactions",
                column: "MunicipalAccountId",
                principalTable: "MunicipalAccounts",
                principalColumn: "Id");
        }
    }
}
