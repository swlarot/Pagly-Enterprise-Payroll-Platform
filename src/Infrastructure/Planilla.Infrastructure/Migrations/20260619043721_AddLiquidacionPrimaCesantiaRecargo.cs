using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiquidacionPrimaCesantiaRecargo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Cesantia",
                table: "Liquidaciones",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IndemnizacionSemanas",
                table: "Liquidaciones",
                type: "numeric(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrimaAntiguedad",
                table: "Liquidaciones",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RecargoArt219",
                table: "Liquidaciones",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cesantia",
                table: "Liquidaciones");

            migrationBuilder.DropColumn(
                name: "IndemnizacionSemanas",
                table: "Liquidaciones");

            migrationBuilder.DropColumn(
                name: "PrimaAntiguedad",
                table: "Liquidaciones");

            migrationBuilder.DropColumn(
                name: "RecargoArt219",
                table: "Liquidaciones");
        }
    }
}
