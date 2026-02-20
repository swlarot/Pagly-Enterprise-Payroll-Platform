// ====================================================================
// Planilla - ReporteSipDto (C-012)
// Para la plataforma SIP de la CSS de Panamá
// ====================================================================

namespace Vorluno.Planilla.Application.DTOs.Reportes;

public record ReporteSipDto(
    string NombreEmpresa, string Ruc,
    string NumeroPlanilla, string Periodo, DateTime FechaGeneracion,
    List<EmpleadoSipItem> Empleados,
    TotalesSip Totales
);

public record EmpleadoSipItem(
    string Cedula, string NombreCompleto,
    decimal SalarioBruto, decimal BaseCss,
    decimal CssEmpleado, decimal CssPatronal,
    decimal SeEmpleado, decimal SePatronal,
    decimal RiesgoProfesional, decimal TotalSip
);

public record TotalesSip(
    decimal TotalSalarios, decimal TotalBaseCss,
    decimal TotalCssEmpleado, decimal TotalCssPatronal,
    decimal TotalSeEmpleado, decimal TotalSePatronal,
    decimal TotalRiesgo, decimal GranTotalSip
);
