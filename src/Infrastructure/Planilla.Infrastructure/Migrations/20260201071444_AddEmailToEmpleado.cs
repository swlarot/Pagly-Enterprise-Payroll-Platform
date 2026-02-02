using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailToEmpleado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Empleados",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Empleados_UserId",
                table: "Empleados",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Empleados_AspNetUsers_UserId",
                table: "Empleados",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Empleados_AspNetUsers_UserId",
                table: "Empleados");

            migrationBuilder.DropIndex(
                name: "IX_Empleados_UserId",
                table: "Empleados");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Empleados");
        }
    }
}
