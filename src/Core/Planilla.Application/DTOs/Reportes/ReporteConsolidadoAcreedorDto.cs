// ====================================================================
// Planilla - DTOs para Reporte Consolidado de Acreedores
// Creado: 2026-02-16
// Descripción: DTOs para el reporte que agrupa deducciones por acreedor
// ====================================================================

namespace Vorluno.Planilla.Application.DTOs.Reportes;

/// <summary>
/// DTO principal para el reporte consolidado de acreedores.
/// Agrupa todos los montos de deducciones por acreedor para facilitar
/// la transferencia bancaria a cada beneficiario.
/// </summary>
public record ReporteConsolidadoAcreedorDto(
    string NombreEmpresa,
    string Ruc,
    string Periodo,
    DateTime FechaGeneracion,
    List<AcreedorConsolidadoItem> Acreedores,
    decimal GranTotalSolicitado,
    decimal GranTotalAplicado,
    decimal GranTotalLimitado,
    int TotalAcreedores
);

/// <summary>
/// Resumen de un acreedor con todos los empleados que le descuentan en la planilla.
/// </summary>
public record AcreedorConsolidadoItem(
    int? AcreedorId,
    string NombreAcreedor,
    string? Identificacion,
    string? Banco,
    string? NumeroCuenta,
    string? TipoAcreedor,
    int CantidadEmpleados,
    decimal TotalSolicitado,
    decimal TotalAplicado,
    decimal TotalLimitado,
    List<DeduccionAcreedorDetalle> Detalle
);

/// <summary>
/// Línea de detalle de un empleado dentro del resumen de un acreedor.
/// </summary>
public record DeduccionAcreedorDetalle(
    string EmpleadoNombre,
    string? EmpleadoCedula,
    string TipoDeduccion,
    string Descripcion,
    decimal MontoSolicitado,
    decimal MontoAplicado,
    decimal MontoLimitado,
    string? RazonLimitacion
);
