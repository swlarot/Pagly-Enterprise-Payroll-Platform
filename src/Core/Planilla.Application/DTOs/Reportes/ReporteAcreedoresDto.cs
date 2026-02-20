// ====================================================================
// Planilla - ReporteAcreedoresDto (C-012)
// Para que RRHH entregue a contabilidad las transferencias bancarias
// ====================================================================

namespace Vorluno.Planilla.Application.DTOs.Reportes;

public record ReporteAcreedoresDto(
    string NombreEmpresa, string Ruc, string Periodo, DateTime FechaGeneracion,
    List<AcreedorPagoItem> Acreedores,
    decimal GranTotal, int TotalAcreedores
);

public record AcreedorPagoItem(
    string NombreAcreedor, string? TipoAcreedor,
    string? Identificacion, string? Banco, string? NumeroCuenta,
    decimal TotalATransferir, int CantidadEmpleados,
    List<EmpleadoAcreedorDetalle> Empleados
);

public record EmpleadoAcreedorDetalle(
    string NombreEmpleado, string? Cedula,
    string TipoDeduccion, string Descripcion,
    decimal MontoSolicitado, decimal MontoAplicado,
    bool FueLimitado, string? RazonLimitacion
);
