using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFrecuenciaCuotaToPrestamo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FrecuenciaCuota",
                table: "Prestamos",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<decimal>(
                name: "HorasExtraExceso",
                table: "PayrollDetails",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoHorasExtraExceso",
                table: "PayrollDetails",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FrecuenciaCuota",
                table: "Prestamos");

            migrationBuilder.DropColumn(
                name: "HorasExtraExceso",
                table: "PayrollDetails");

            migrationBuilder.DropColumn(
                name: "MontoHorasExtraExceso",
                table: "PayrollDetails");
        }
    }
}
