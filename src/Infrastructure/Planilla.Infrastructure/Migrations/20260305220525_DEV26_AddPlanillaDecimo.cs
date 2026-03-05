using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DEV26_AddPlanillaDecimo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanillasDecimo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Numero = table.Column<string>(type: "text", nullable: false),
                    PeriodoDesde = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodoHasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaPago = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    TotalDevengado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalDecimo = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCssEmpleado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCssPatrono = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalSeEmpleado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalSePatrono = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalISR = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalNetoPago = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanillasDecimo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanillasDecimo_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DetallesDecimo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    PlanillaDecimoId = table.Column<int>(type: "integer", nullable: false),
                    EmpleadoId = table.Column<int>(type: "integer", nullable: false),
                    DesgloseMensualJson = table.Column<string>(type: "text", nullable: false),
                    TotalDevengado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MontoDecimo = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CssEmpleado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CssPatrono = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SeEmpleado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SePatrono = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ISR = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalDeducciones = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetoPago = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesDecimo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallesDecimo_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesDecimo_PlanillasDecimo_PlanillaDecimoId",
                        column: x => x.PlanillaDecimoId,
                        principalTable: "PlanillasDecimo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesDecimo_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesDecimo_EmpleadoId",
                table: "DetallesDecimo",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesDecimo_PlanillaDecimoId",
                table: "DetallesDecimo",
                column: "PlanillaDecimoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesDecimo_TenantId",
                table: "DetallesDecimo",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanillasDecimo_TenantId",
                table: "PlanillasDecimo",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetallesDecimo");

            migrationBuilder.DropTable(
                name: "PlanillasDecimo");
        }
    }
}
