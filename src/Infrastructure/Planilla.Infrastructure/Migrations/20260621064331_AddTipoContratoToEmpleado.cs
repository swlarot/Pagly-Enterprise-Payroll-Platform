using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTipoContratoToEmpleado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TipoContrato",
                table: "Empleados",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoContrato",
                table: "Empleados");
        }
    }
}
