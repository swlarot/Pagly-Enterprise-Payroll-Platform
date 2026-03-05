using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DEV42_AddSalarioVacacionalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MontoVacaciones",
                table: "SolicitudesVacaciones",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodoReferenciaDesde",
                table: "SolicitudesVacaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodoReferenciaHasta",
                table: "SolicitudesVacaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalarioVacacional",
                table: "SolicitudesVacaciones",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MontoVacaciones",
                table: "SolicitudesVacaciones");

            migrationBuilder.DropColumn(
                name: "PeriodoReferenciaDesde",
                table: "SolicitudesVacaciones");

            migrationBuilder.DropColumn(
                name: "PeriodoReferenciaHasta",
                table: "SolicitudesVacaciones");

            migrationBuilder.DropColumn(
                name: "SalarioVacacional",
                table: "SolicitudesVacaciones");
        }
    }
}
