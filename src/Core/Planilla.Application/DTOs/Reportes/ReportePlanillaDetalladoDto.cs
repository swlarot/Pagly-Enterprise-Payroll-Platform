// ====================================================================
// Planilla - DTOs para Reporte de Planilla Detallado
// Creado: 2025-12-28
// Descripción: DTOs para generar reportes detallados de planilla completa
// ====================================================================

namespace Vorluno.Planilla.Application.DTOs.Reportes;

/// <summary>
/// DTO principal para el reporte de planilla detallado
/// </summary>
public record ReportePlanillaDetalladoDto(
    // Encabezado
    string NombreEmpresa,
    string Ruc,
    string NumeroPlanilla,
    string Periodo,
    DateTime FechaPago,
    string Estado,
    DateTime FechaGeneracion,

    // Resumen
    int TotalEmpleados,
    decimal TotalBruto,
    decimal TotalDeducciones,
    decimal TotalNeto,
    decimal TotalCostoPatronal,

    // Detalle
    List<EmpleadoPlanillaDetalladoDto> Empleados,

    // Por departamento (opcional)
    List<ResumenDepartamentoDto>? ResumenPorDepartamento
);

/// <summary>
/// DTO para los datos completos de un empleado en la planilla
/// </summary>
public record EmpleadoPlanillaDetalladoDto(
    string Cedula,
    string NombreCompleto,
    string? Departamento,
    string? Posicion,

    // Ingresos
    decimal SalarioBase,
    decimal HorasExtra,
    decimal Bonificaciones,
    decimal SalarioBruto,

    // Deducciones
    decimal CssEmpleado,
    decimal SeEmpleado,
    decimal Isr,
    decimal Prestamos,
    decimal Anticipos,
    decimal DeduccionesFijas,
    decimal DescuentoAusencias,
    decimal OtrasDeducciones,
    decimal TotalDeducciones,

    // Neto
    decimal SalarioNeto,

    // Costos patronales
    decimal CssPatrono,
    decimal SePatrono,
    decimal RiesgoProfesional,
    decimal CostoPatronal,

    // Desglose deducciones adicionales
    decimal PensionAlimenticia = 0,
    decimal Embargos = 0,
    decimal DeduccionesVoluntarias = 0,
    bool TuvoLimitacionSalarioMinimo = false
);

/// <summary>
/// DTO para resumen por departamento
/// </summary>
public record ResumenDepartamentoDto(
    string NombreDepartamento,
    int CantidadEmpleados,
    decimal TotalBruto,
    decimal TotalDeducciones,
    decimal TotalNeto,
    decimal TotalCostoPatronal
);
