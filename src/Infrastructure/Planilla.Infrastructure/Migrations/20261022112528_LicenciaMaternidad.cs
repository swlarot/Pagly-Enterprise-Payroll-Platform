using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LicenciaMaternidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nota",
                table: "PayrollDetails",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LicenciasMaternidad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Observaciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreadaPor = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Anulada = table.Column<bool>(type: "boolean", nullable: false),
                    AnuladaPor = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    AnuladaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenciasMaternidad", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LicenciasMaternidad_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LicenciasMaternidad_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LicenciaMaternidad_Tenant_Empleado_Inicio",
                table: "LicenciasMaternidad",
                columns: new[] { "TenantId", "EmpleadoId", "FechaInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_LicenciasMaternidad_EmpleadoId",
                table: "LicenciasMaternidad",
                column: "EmpleadoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LicenciasMaternidad");

            migrationBuilder.DropColumn(
                name: "Nota",
                table: "PayrollDetails");
        }
    }
}
