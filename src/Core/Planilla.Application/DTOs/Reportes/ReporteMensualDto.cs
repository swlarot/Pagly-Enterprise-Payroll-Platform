// ====================================================================
// Planilla - ReporteMensualDto (C-012)
// Consolida todas las planillas del mes para reporte mensual
// ====================================================================

namespace Vorluno.Planilla.Application.DTOs.Reportes;

public record ReporteMensualDto(
    string NombreEmpresa, string Ruc,
    int Mes, int Anio, string NombreMes,
    List<string> PeriodosIncluidos,
    List<EmpleadoMensualItem> Empleados,
    TotalesMensual Totales
);

public record EmpleadoMensualItem(
    string Cedula, string NombreCompleto,
    decimal TotalBruto, decimal TotalCss, decimal TotalSe, decimal TotalIsr,
    decimal TotalAcreedores,
    decimal TotalPensionAlimenticia, decimal TotalEmbargos, decimal TotalDeduccionesVoluntarias,
    decimal TotalDeducciones,
    decimal TotalNeto
);

public record TotalesMensual(
    int TotalEmpleados, decimal GranTotalBruto,
    decimal GranTotalCss, decimal GranTotalSe, decimal GranTotalIsr,
    decimal GranTotalAcreedores,
    decimal GranTotalPensionAlimenticia, decimal GranTotalEmbargos, decimal GranTotalDeduccionesVoluntarias,
    decimal GranTotalDeducciones,
    decimal GranTotalNeto
);
