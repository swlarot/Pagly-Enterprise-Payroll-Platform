using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HorasExtraDecisionDirector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRechazo",
                table: "HorasExtra",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoRechazo",
                table: "HorasExtra",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RechazadoPor",
                table: "HorasExtra",
                type: "text",
                nullable: true);

            // Aprobar horas extra pasa a ser un permiso propio (el del director).
            // Los roles que hoy las gestionan lo reciben, para que nadie pierda
            // lo que ya podía hacer; el dueño decide después a quién quitárselo.
            migrationBuilder.Sql(@"
                INSERT INTO ""RolePermissions"" (""TenantId"", ""CustomTenantRoleId"", ""Permission"", ""GrantedAt"", ""CreatedAt"", ""IsActive"")
                SELECT rp.""TenantId"", rp.""CustomTenantRoleId"", 'overtime.approve', NOW(), NOW(), TRUE
                FROM ""RolePermissions"" rp
                WHERE rp.""Permission"" = 'overtime.manage'
                  AND rp.""IsActive"" = TRUE
                  AND NOT EXISTS (
                      SELECT 1 FROM ""RolePermissions"" x
                      WHERE x.""CustomTenantRoleId"" = rp.""CustomTenantRoleId""
                        AND x.""Permission"" = 'overtime.approve');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaRechazo",
                table: "HorasExtra");

            migrationBuilder.DropColumn(
                name: "MotivoRechazo",
                table: "HorasExtra");

            migrationBuilder.DropColumn(
                name: "RechazadoPor",
                table: "HorasExtra");
        }
    }
}
