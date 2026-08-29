using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOvertimeFactorConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OvertimeFactorConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Factor = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    FactorExceso = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    EsFactorExceso = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OvertimeFactorConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OvertimeFactorConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeFactorConfiguration_TenantId_Tipo",
                table: "OvertimeFactorConfigurations",
                columns: new[] { "TenantId", "Tipo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OvertimeFactorConfigurations");
        }
    }
}
