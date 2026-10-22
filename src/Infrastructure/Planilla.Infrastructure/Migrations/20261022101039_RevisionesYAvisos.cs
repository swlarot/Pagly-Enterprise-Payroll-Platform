using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RevisionesYAvisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Avisos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Mensaje = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Enlace = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SolicitudRevisionId = table.Column<int>(type: "integer", nullable: true),
                    Leido = table.Column<bool>(type: "boolean", nullable: false),
                    LeidoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Avisos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Avisos_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SolicitudesRevision",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    EntidadId = table.Column<int>(type: "integer", nullable: false),
                    Referencia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Enlace = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SolicitadoPor = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RevisorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Respuesta = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RespondidaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesRevision", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitudesRevision_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aviso_Tenant_Usuario_Leido_Fecha",
                table: "Avisos",
                columns: new[] { "TenantId", "UsuarioId", "Leido", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudRevision_Tenant_Revisor_Estado",
                table: "SolicitudesRevision",
                columns: new[] { "TenantId", "RevisorId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudRevision_Tenant_Tipo_Entidad",
                table: "SolicitudesRevision",
                columns: new[] { "TenantId", "Tipo", "EntidadId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Avisos");

            migrationBuilder.DropTable(
                name: "SolicitudesRevision");
        }
    }
}
