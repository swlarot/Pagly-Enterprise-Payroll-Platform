// ====================================================================
// Planilla - DTOs para Reporte de Deducciones por Empleado
// Creado: 2026-02-16
// Descripción: DTOs para el reporte que muestra la cascada de deducciones
//              adicionales aplicadas a cada empleado, incluyendo limitaciones
//              por salario mínimo.
// ====================================================================

namespace Vorluno.Planilla.Application.DTOs.Reportes;

/// <summary>
/// DTO principal para el reporte de deducciones adicionales por empleado.
/// Muestra la cascada completa de deducciones (pensiones, embargos, voluntarias)
/// con el orden de prelación y las razones de limitación si aplica.
/// </summary>
public record ReporteDeduccionesEmpleadoDto(
    string NombreEmpresa,
    string Ruc,
    string Periodo,
    DateTime FechaGeneracion,
    List<EmpleadoDeduccionesItem> Empleados,
    int EmpleadosConDeducciones,
    int EmpleadosConLimitacion,
    decimal TotalPensiones,
    decimal TotalEmbargos,
    decimal TotalVoluntarias,
    decimal TotalLimitado
);

/// <summary>
/// Datos completos de las deducciones adicionales de un empleado en el período.
/// </summary>
public record EmpleadoDeduccionesItem(
    int EmpleadoId,
    string Nombre,
    string? Cedula,
    string? Departamento,
    string? Posicion,
    decimal SalarioBruto,
    decimal DeduccionesLegales,
    decimal NetoPostLegal,
    decimal SalarioMinimoAplicado,
    List<DeduccionEmpleadoDetalle> Deducciones,
    decimal TotalDeduccionesAdicionales,
    decimal NetPayFinal,
    bool TuvoLimitacion
);

/// <summary>
/// Línea individual de la cascada de deducciones para un empleado.
/// Incluye datos de auditoría: saldos antes/después y razón de limitación.
/// </summary>
public record DeduccionEmpleadoDetalle(
    int Orden,
    string Categoria,
    string TipoDeduccion,
    string Descripcion,
    string? NombreAcreedor,
    decimal MontoSolicitado,
    decimal MontoAplicado,
    decimal MontoLimitado,
    string? RazonLimitacion,
    decimal SaldoDisponibleAntes,
    decimal SaldoDisponibleDespues
);
