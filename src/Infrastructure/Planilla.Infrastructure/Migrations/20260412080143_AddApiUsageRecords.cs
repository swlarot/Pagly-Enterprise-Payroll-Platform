using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiUsageRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiUsageRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ApiKeyId = table.Column<int>(type: "integer", nullable: true),
                    Endpoint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiUsageRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiUsageRecords_ApiKeys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "ApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiUsageRecord_ApiKeyId_CreatedAt",
                table: "ApiUsageRecords",
                columns: new[] { "ApiKeyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiUsageRecord_TenantId_CreatedAt",
                table: "ApiUsageRecords",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiUsageRecords");
        }
    }
}
