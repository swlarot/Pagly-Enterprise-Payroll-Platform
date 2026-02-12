using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayPeriodTypeAndHoursTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PayPeriodType",
                table: "PayrollHeaders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyRate",
                table: "Empleados",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HoursPerPeriod",
                table: "Empleados",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "HoursPerWeek",
                table: "Empleados",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PayPeriodType",
                table: "Empleados",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Poblar valores default para datos existentes
            migrationBuilder.Sql(@"
                UPDATE ""Empleados"" SET
                    ""PayPeriodType"" = 2,
                    ""HoursPerWeek"" = 48,
                    ""HoursPerPeriod"" = 104,
                    ""HourlyRate"" = CASE WHEN 104 > 0 THEN ROUND(""SalarioBase"" / 104, 4) ELSE 0 END;

                UPDATE ""PayrollHeaders"" SET ""PayPeriodType"" = 2;
            ");

            migrationBuilder.CreateTable(
                name: "PayrollEmployeeHours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollHeaderId = table.Column<int>(type: "integer", nullable: false),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    RegularHours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    SundayHours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    HolidayHours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    OvertimeDayHours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    OvertimeNightHours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    AbsenceHours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    DisabilityHours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    RegularPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SundayPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HolidayPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OvertimeDayPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OvertimeNightPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AbsenceDeduction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalHoursPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollEmployeeHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollEmployeeHours_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollEmployeeHours_PayrollHeaders_PayrollHeaderId",
                        column: x => x.PayrollHeaderId,
                        principalTable: "PayrollHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollEmployeeHours_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollHeaders_TenantId_PayPeriodType",
                table: "PayrollHeaders",
                columns: new[] { "TenantId", "PayPeriodType" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeHours_EmpleadoId",
                table: "PayrollEmployeeHours",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeHours_HeaderId_EmpleadoId",
                table: "PayrollEmployeeHours",
                columns: new[] { "PayrollHeaderId", "EmpleadoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeHours_TenantId",
                table: "PayrollEmployeeHours",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollEmployeeHours");

            migrationBuilder.DropIndex(
                name: "IX_PayrollHeaders_TenantId_PayPeriodType",
                table: "PayrollHeaders");

            migrationBuilder.DropColumn(
                name: "PayPeriodType",
                table: "PayrollHeaders");

            migrationBuilder.DropColumn(
                name: "HourlyRate",
                table: "Empleados");

            migrationBuilder.DropColumn(
                name: "HoursPerPeriod",
                table: "Empleados");

            migrationBuilder.DropColumn(
                name: "HoursPerWeek",
                table: "Empleados");

            migrationBuilder.DropColumn(
                name: "PayPeriodType",
                table: "Empleados");
        }
    }
}
