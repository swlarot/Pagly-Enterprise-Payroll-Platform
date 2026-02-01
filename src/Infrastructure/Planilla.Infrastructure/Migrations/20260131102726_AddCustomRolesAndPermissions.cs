using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomRolesAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomTenantRoleId",
                table: "TenantUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomTenantRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomTenantRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomTenantRoles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CustomTenantRoleId = table.Column<int>(type: "integer", nullable: false),
                    Permission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_CustomTenantRoles_CustomTenantRoleId",
                        column: x => x.CustomTenantRoleId,
                        principalTable: "CustomTenantRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_CustomTenantRoleId",
                table: "TenantUsers",
                column: "CustomTenantRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomTenantRole_TenantId_IsSystem",
                table: "CustomTenantRoles",
                columns: new[] { "TenantId", "IsSystem" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomTenantRole_TenantId_Name",
                table: "CustomTenantRoles",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_RoleId_Permission",
                table: "RolePermissions",
                columns: new[] { "CustomTenantRoleId", "Permission" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_TenantId",
                table: "RolePermissions",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_TenantUsers_CustomTenantRoles_CustomTenantRoleId",
                table: "TenantUsers",
                column: "CustomTenantRoleId",
                principalTable: "CustomTenantRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TenantUsers_CustomTenantRoles_CustomTenantRoleId",
                table: "TenantUsers");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "CustomTenantRoles");

            migrationBuilder.DropIndex(
                name: "IX_TenantUsers_CustomTenantRoleId",
                table: "TenantUsers");

            migrationBuilder.DropColumn(
                name: "CustomTenantRoleId",
                table: "TenantUsers");
        }
    }
}
