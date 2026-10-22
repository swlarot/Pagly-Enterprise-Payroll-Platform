using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RelojVinculacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EsperandoVinculoHasta",
                table: "DispositivosReloj",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpPendiente",
                table: "DispositivosReloj",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpRechazada",
                table: "DispositivosReloj",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IpRechazadaEn",
                table: "DispositivosReloj",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpVinculada",
                table: "DispositivosReloj",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VinculadoEn",
                table: "DispositivosReloj",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsperandoVinculoHasta",
                table: "DispositivosReloj");

            migrationBuilder.DropColumn(
                name: "IpPendiente",
                table: "DispositivosReloj");

            migrationBuilder.DropColumn(
                name: "IpRechazada",
                table: "DispositivosReloj");

            migrationBuilder.DropColumn(
                name: "IpRechazadaEn",
                table: "DispositivosReloj");

            migrationBuilder.DropColumn(
                name: "IpVinculada",
                table: "DispositivosReloj");

            migrationBuilder.DropColumn(
                name: "VinculadoEn",
                table: "DispositivosReloj");
        }
    }
}
