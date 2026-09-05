using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAcumuladoFiscalEmpleado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcumuladosFiscalesEmpleados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    Anio = table.Column<int>(type: "integer", nullable: false),
                    IngresoGravableInicial = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DecimoInicial = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsrRetenidoInicial = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IngresoGravableProcesado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DecimoProcesado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsrRegularProcesado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsrDecimoProcesado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SaldoFavorEmpleado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PeriodosProcesados = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcumuladosFiscalesEmpleados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcumuladosFiscalesEmpleados_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AcumuladosFiscalesEmpleados_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcumuladoFiscalEmpleado_Tenant_Empleado_Anio",
                table: "AcumuladosFiscalesEmpleados",
                columns: new[] { "TenantId", "EmpleadoId", "Anio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcumuladosFiscalesEmpleados_EmpleadoId",
                table: "AcumuladosFiscalesEmpleados",
                column: "EmpleadoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcumuladosFiscalesEmpleados");
        }
    }
}
