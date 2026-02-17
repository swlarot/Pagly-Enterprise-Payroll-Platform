using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditorDeductionsAndMinWageProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActividadEconomica",
                table: "PayrollTaxConfigurations",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalarioMinimoLegal",
                table: "PayrollTaxConfigurations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeduccionesVoluntarias",
                table: "PayrollDetails",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Embargos",
                table: "PayrollDetails",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoLimitadoPorSalarioMinimo",
                table: "PayrollDetails",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PensionAlimenticia",
                table: "PayrollDetails",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SalarioMinimoLegalAplicado",
                table: "PayrollDetails",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "TuvoLimitacionSalarioMinimo",
                table: "PayrollDetails",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AplicaADecimoTercerMes",
                table: "DeduccionesFijas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AplicaAPrestaciones",
                table: "DeduccionesFijas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BancoAcreedor",
                table: "DeduccionesFijas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseCalculo",
                table: "DeduccionesFijas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Categoria",
                table: "DeduccionesFijas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CuentaBancariaAcreedor",
                table: "DeduccionesFijas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentoAutorizacionRef",
                table: "DeduccionesFijas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstadoOrdenJudicial",
                table: "DeduccionesFijas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAutorizacion",
                table: "DeduccionesFijas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaLevantamiento",
                table: "DeduccionesFijas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaOrdenJudicial",
                table: "DeduccionesFijas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentificacionAcreedor",
                table: "DeduccionesFijas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Juzgado",
                table: "DeduccionesFijas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoCobradoAcumulado",
                table: "DeduccionesFijas",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoTotalACobrar",
                table: "DeduccionesFijas",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoLevantamiento",
                table: "DeduccionesFijas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreAcreedor",
                table: "DeduccionesFijas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreJuez",
                table: "DeduccionesFijas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroExpediente",
                table: "DeduccionesFijas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TieneAutorizacionEscrita",
                table: "DeduccionesFijas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DeduccionesAplicadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    PayrollDetailId = table.Column<int>(type: "integer", nullable: false),
                    DeduccionFijaId = table.Column<int>(type: "integer", nullable: true),
                    PrestamoId = table.Column<int>(type: "integer", nullable: true),
                    AnticipoId = table.Column<int>(type: "integer", nullable: true),
                    TipoDeduccion = table.Column<int>(type: "integer", nullable: false),
                    Categoria = table.Column<int>(type: "integer", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    MontoSolicitado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MontoAplicado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MontoLimitado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RazonLimitacion = table.Column<string>(type: "text", nullable: true),
                    SaldoDisponibleAntes = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SaldoDisponibleDespues = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OrdenAplicacion = table.Column<int>(type: "integer", nullable: false),
                    NombreAcreedor = table.Column<string>(type: "text", nullable: true),
                    FechaTransferencia = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenciaTransferencia = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeduccionesAplicadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeduccionesAplicadas_Anticipos_AnticipoId",
                        column: x => x.AnticipoId,
                        principalTable: "Anticipos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DeduccionesAplicadas_DeduccionesFijas_DeduccionFijaId",
                        column: x => x.DeduccionFijaId,
                        principalTable: "DeduccionesFijas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DeduccionesAplicadas_PayrollDetails_PayrollDetailId",
                        column: x => x.PayrollDetailId,
                        principalTable: "PayrollDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeduccionesAplicadas_Prestamos_PrestamoId",
                        column: x => x.PrestamoId,
                        principalTable: "Prestamos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DeduccionesAplicadas_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeduccionAplicada_DeduccionFijaId",
                table: "DeduccionesAplicadas",
                column: "DeduccionFijaId");

            migrationBuilder.CreateIndex(
                name: "IX_DeduccionAplicada_PayrollDetailId",
                table: "DeduccionesAplicadas",
                column: "PayrollDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_DeduccionAplicada_TenantId_PayrollDetailId",
                table: "DeduccionesAplicadas",
                columns: new[] { "TenantId", "PayrollDetailId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeduccionesAplicadas_AnticipoId",
                table: "DeduccionesAplicadas",
                column: "AnticipoId");

            migrationBuilder.CreateIndex(
                name: "IX_DeduccionesAplicadas_PrestamoId",
                table: "DeduccionesAplicadas",
                column: "PrestamoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeduccionesAplicadas");

            migrationBuilder.DropColumn(
                name: "ActividadEconomica",
                table: "PayrollTaxConfigurations");

            migrationBuilder.DropColumn(
                name: "SalarioMinimoLegal",
                table: "PayrollTaxConfigurations");

            migrationBuilder.DropColumn(
                name: "DeduccionesVoluntarias",
                table: "PayrollDetails");

            migrationBuilder.DropColumn(
                name: "Embargos",
                table: "PayrollDetails");

            migrationBuilder.DropColumn(
                name: "MontoLimitadoPorSalarioMinimo",
                table: "PayrollDetails");

            migrationBuilder.DropColumn(
                name: "PensionAlimenticia",
                table: "PayrollDetails");

            migrationBuilder.DropColumn(
                name: "SalarioMinimoLegalAplicado",
                table: "PayrollDetails");

            migrationBuilder.DropColumn(
                name: "TuvoLimitacionSalarioMinimo",
                table: "PayrollDetails");

            migrationBuilder.DropColumn(
                name: "AplicaADecimoTercerMes",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "AplicaAPrestaciones",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "BancoAcreedor",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "BaseCalculo",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "CuentaBancariaAcreedor",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "DocumentoAutorizacionRef",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "EstadoOrdenJudicial",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "FechaAutorizacion",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "FechaLevantamiento",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "FechaOrdenJudicial",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "IdentificacionAcreedor",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "Juzgado",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "MontoCobradoAcumulado",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "MontoTotalACobrar",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "MotivoLevantamiento",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "NombreAcreedor",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "NombreJuez",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "NumeroExpediente",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "TieneAutorizacionEscrita",
                table: "DeduccionesFijas");
        }
    }
}
