using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_RecibosDeSueldo_TenantId",
                table: "RecibosDeSueldo",
                newName: "IX_ReciboDeSueldo_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_PagosPrestamos_TenantId",
                table: "PagosPrestamos",
                newName: "IX_PagoPrestamo_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Empleados_TenantId",
                table: "Empleados",
                newName: "IX_Empleado_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Empleados_NumeroIdentificacion",
                table: "Empleados",
                newName: "IX_Empleado_NumeroIdentificacion");

            migrationBuilder.CreateIndex(
                name: "IX_ReciboDeSueldo_TenantId_EmpleadoId",
                table: "RecibosDeSueldo",
                columns: new[] { "TenantId", "EmpleadoId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReciboDeSueldo_TenantId_FechaGeneracion",
                table: "RecibosDeSueldo",
                columns: new[] { "TenantId", "FechaGeneracion" });

            migrationBuilder.CreateIndex(
                name: "IX_Empleado_TenantId_DepartamentoId",
                table: "Empleados",
                columns: new[] { "TenantId", "DepartamentoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Empleado_TenantId_EstaActivo",
                table: "Empleados",
                columns: new[] { "TenantId", "EstaActivo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReciboDeSueldo_TenantId_EmpleadoId",
                table: "RecibosDeSueldo");

            migrationBuilder.DropIndex(
                name: "IX_ReciboDeSueldo_TenantId_FechaGeneracion",
                table: "RecibosDeSueldo");

            migrationBuilder.DropIndex(
                name: "IX_Empleado_TenantId_DepartamentoId",
                table: "Empleados");

            migrationBuilder.DropIndex(
                name: "IX_Empleado_TenantId_EstaActivo",
                table: "Empleados");

            migrationBuilder.RenameIndex(
                name: "IX_ReciboDeSueldo_TenantId",
                table: "RecibosDeSueldo",
                newName: "IX_RecibosDeSueldo_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_PagoPrestamo_TenantId",
                table: "PagosPrestamos",
                newName: "IX_PagosPrestamos_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Empleado_TenantId",
                table: "Empleados",
                newName: "IX_Empleados_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Empleado_NumeroIdentificacion",
                table: "Empleados",
                newName: "IX_Empleados_NumeroIdentificacion");
        }
    }
}
