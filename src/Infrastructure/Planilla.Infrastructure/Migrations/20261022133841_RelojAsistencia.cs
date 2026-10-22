using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RelojAsistencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Origen",
                table: "HorasExtra",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoReloj",
                table: "Empleados",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HorarioLaboralId",
                table: "Empleados",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HorariosLaborales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HoraEntrada = table.Column<TimeSpan>(type: "interval", nullable: false),
                    HoraSalida = table.Column<TimeSpan>(type: "interval", nullable: false),
                    DiasLaborables = table.Column<int>(type: "integer", nullable: false),
                    ToleranciaMinutos = table.Column<int>(type: "integer", nullable: false),
                    MinutosParaHoraExtra = table.Column<int>(type: "integer", nullable: false),
                    EsPredeterminado = table.Column<bool>(type: "boolean", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HorariosLaborales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HorariosLaborales_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Marcaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CodigoReloj = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: true),
                    FechaHora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Origen = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Fuente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marcaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Marcaciones_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Marcaciones_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Empleado_Tenant_CodigoReloj",
                table: "Empleados",
                columns: new[] { "TenantId", "CodigoReloj" },
                unique: true,
                filter: "\"CodigoReloj\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Empleados_HorarioLaboralId",
                table: "Empleados",
                column: "HorarioLaboralId");

            migrationBuilder.CreateIndex(
                name: "IX_HorarioLaboral_Tenant",
                table: "HorariosLaborales",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Marcacion_Tenant_Codigo_FechaHora",
                table: "Marcaciones",
                columns: new[] { "TenantId", "CodigoReloj", "FechaHora" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Marcacion_Tenant_Empleado_FechaHora",
                table: "Marcaciones",
                columns: new[] { "TenantId", "EmpleadoId", "FechaHora" });

            migrationBuilder.CreateIndex(
                name: "IX_Marcaciones_EmpleadoId",
                table: "Marcaciones",
                column: "EmpleadoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Empleados_HorariosLaborales_HorarioLaboralId",
                table: "Empleados",
                column: "HorarioLaboralId",
                principalTable: "HorariosLaborales",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Empleados_HorariosLaborales_HorarioLaboralId",
                table: "Empleados");

            migrationBuilder.DropTable(
                name: "HorariosLaborales");

            migrationBuilder.DropTable(
                name: "Marcaciones");

            migrationBuilder.DropIndex(
                name: "IX_Empleado_Tenant_CodigoReloj",
                table: "Empleados");

            migrationBuilder.DropIndex(
                name: "IX_Empleados_HorarioLaboralId",
                table: "Empleados");

            migrationBuilder.DropColumn(
                name: "Origen",
                table: "HorasExtra");

            migrationBuilder.DropColumn(
                name: "CodigoReloj",
                table: "Empleados");

            migrationBuilder.DropColumn(
                name: "HorarioLaboralId",
                table: "Empleados");
        }
    }
}
