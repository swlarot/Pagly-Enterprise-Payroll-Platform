using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAcreedorCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcreedorId",
                table: "Prestamos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AcreedorId",
                table: "DeduccionesFijas",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Acreedores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TipoAcreedor = table.Column<int>(type: "integer", nullable: false),
                    Identificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TipoIdentificacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Banco = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TipoCuenta = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NumeroCuenta = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IBAN = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Telefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ContactoNombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EstaActivo = table.Column<bool>(type: "boolean", nullable: false),
                    Observaciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acreedores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acreedores_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Prestamos_AcreedorId",
                table: "Prestamos",
                column: "AcreedorId");

            migrationBuilder.CreateIndex(
                name: "IX_DeduccionesFijas_AcreedorId",
                table: "DeduccionesFijas",
                column: "AcreedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Acreedor_TenantId_Identificacion",
                table: "Acreedores",
                columns: new[] { "TenantId", "Identificacion" });

            migrationBuilder.CreateIndex(
                name: "IX_Acreedor_TenantId_Nombre",
                table: "Acreedores",
                columns: new[] { "TenantId", "Nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_Acreedor_TenantId_TipoAcreedor",
                table: "Acreedores",
                columns: new[] { "TenantId", "TipoAcreedor" });

            migrationBuilder.AddForeignKey(
                name: "FK_DeduccionesFijas_Acreedores_AcreedorId",
                table: "DeduccionesFijas",
                column: "AcreedorId",
                principalTable: "Acreedores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Prestamos_Acreedores_AcreedorId",
                table: "Prestamos",
                column: "AcreedorId",
                principalTable: "Acreedores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeduccionesFijas_Acreedores_AcreedorId",
                table: "DeduccionesFijas");

            migrationBuilder.DropForeignKey(
                name: "FK_Prestamos_Acreedores_AcreedorId",
                table: "Prestamos");

            migrationBuilder.DropTable(
                name: "Acreedores");

            migrationBuilder.DropIndex(
                name: "IX_Prestamos_AcreedorId",
                table: "Prestamos");

            migrationBuilder.DropIndex(
                name: "IX_DeduccionesFijas_AcreedorId",
                table: "DeduccionesFijas");

            migrationBuilder.DropColumn(
                name: "AcreedorId",
                table: "Prestamos");

            migrationBuilder.DropColumn(
                name: "AcreedorId",
                table: "DeduccionesFijas");
        }
    }
}
