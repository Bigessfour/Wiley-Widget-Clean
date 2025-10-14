using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WileyWidget.Data.Migrations
{
    /// <inheritdoc />
    public partial class PendingChangesFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EnterpriseId",
                table: "BudgetInteraction",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetInteraction_EnterpriseId",
                table: "BudgetInteraction",
                column: "EnterpriseId");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetInteraction_Enterprises_EnterpriseId",
                table: "BudgetInteraction",
                column: "EnterpriseId",
                principalTable: "Enterprises",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetInteraction_Enterprises_EnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropIndex(
                name: "IX_BudgetInteraction_EnterpriseId",
                table: "BudgetInteraction");

            migrationBuilder.DropColumn(
                name: "EnterpriseId",
                table: "BudgetInteraction");
        }
    }
}
