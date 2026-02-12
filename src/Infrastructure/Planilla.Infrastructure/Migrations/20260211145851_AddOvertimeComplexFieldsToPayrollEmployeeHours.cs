using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOvertimeComplexFieldsToPayrollEmployeeHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeExcessHours",
                table: "PayrollEmployeeHours",
                type: "numeric(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeExcessPay",
                table: "PayrollEmployeeHours",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeHolidayHours",
                table: "PayrollEmployeeHours",
                type: "numeric(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeHolidayPay",
                table: "PayrollEmployeeHours",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeMixedHours",
                table: "PayrollEmployeeHours",
                type: "numeric(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeMixedPay",
                table: "PayrollEmployeeHours",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OvertimeExcessHours",
                table: "PayrollEmployeeHours");

            migrationBuilder.DropColumn(
                name: "OvertimeExcessPay",
                table: "PayrollEmployeeHours");

            migrationBuilder.DropColumn(
                name: "OvertimeHolidayHours",
                table: "PayrollEmployeeHours");

            migrationBuilder.DropColumn(
                name: "OvertimeHolidayPay",
                table: "PayrollEmployeeHours");

            migrationBuilder.DropColumn(
                name: "OvertimeMixedHours",
                table: "PayrollEmployeeHours");

            migrationBuilder.DropColumn(
                name: "OvertimeMixedPay",
                table: "PayrollEmployeeHours");
        }
    }
}
