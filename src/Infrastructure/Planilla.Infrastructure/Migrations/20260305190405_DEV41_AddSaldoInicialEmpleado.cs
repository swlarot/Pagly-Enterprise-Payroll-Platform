using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DEV41_AddSaldoInicialEmpleado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SaldosInicialesEmpleados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    FechaCorte = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DiasVacacionesAcumulados = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    DiasVacacionesTomados = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    MontoDecimoAcumulado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaldosInicialesEmpleados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaldosInicialesEmpleados_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SaldosInicialesEmpleados_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaldosInicialesEmpleados_EmpleadoId",
                table: "SaldosInicialesEmpleados",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_SaldosInicialesEmpleados_TenantId",
                table: "SaldosInicialesEmpleados",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaldosInicialesEmpleados");
        }
    }
}
