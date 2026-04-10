// ====================================================================
// Planilla - ReportePlanillaRegularDto (C-012)
// Reporte operativo por período — borrador de planilla regular
// ====================================================================

namespace Vorluno.Planilla.Application.DTOs.Reportes;

public record ReportePlanillaRegularDto(
    string NombreEmpresa, string Ruc, string NumeroPlanilla,
    string Periodo, DateTime FechaPago, string Estado,
    List<EmpleadoPlanillaRegularItem> Empleados,
    TotalesPlanillaRegular Totales,
    bool EsSinDeducciones = false
);

public record LineaDesgloseHoras(
    string TipoConcepto,
    decimal Horas,
    decimal TarifaPorHora,
    decimal Valor
);

public record EmpleadoPlanillaRegularItem(
    string Cedula, string NombreCompleto,
    decimal HorasRegulares, decimal HorasDomingo, decimal HorasFeriado, decimal HorasExtra,
    decimal HorasExtraExceso, decimal MontoHorasExtraExceso,
    decimal SalarioBruto, decimal CssEmpleado, decimal SeEmpleado, decimal Isr,
    decimal TotalAcreedores,
    decimal PensionAlimenticia, decimal Embargos, decimal DeduccionesVoluntarias,
    decimal TotalDeducciones,
    decimal SalarioNeto,
    bool TuvoLimitacion, string? RazonLimitacion,
    List<LineaDesgloseHoras> DesgloseHoras
);

public record TotalesPlanillaRegular(
    int TotalEmpleados, decimal TotalBruto,
    decimal TotalCss, decimal TotalSe, decimal TotalIsr,
    decimal TotalAcreedores,
    decimal TotalPensionAlimenticia, decimal TotalEmbargos, decimal TotalDeduccionesVoluntarias,
    decimal TotalDeducciones,
    decimal TotalNeto,
    decimal TotalHorasExtraExceso, decimal TotalMontoHorasExtraExceso
);
