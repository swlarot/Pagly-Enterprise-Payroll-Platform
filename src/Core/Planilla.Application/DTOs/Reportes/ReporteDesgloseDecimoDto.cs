// ====================================================================
// Planilla - ReporteDesgloseDecimoDto (DEV-171)
// Reporte de desglose del Décimo Tercer Mes por período de pago real
// ====================================================================

namespace Vorluno.Planilla.Application.DTOs.Reportes;

public record ReporteDesgloseDecimoDto(
    string NombreEmpresa,
    string Ruc,
    string NumeroDecimo,
    string Periodo,
    DateTime FechaPago,
    string Estado,
    int Partida,
    List<EmpleadoDesgloseDecimoItem> Empleados,
    TotalesDesgloseDecimo Totales
);

public record PeriodoPagoDecimoItem(
    string NumeroPlanilla,
    DateTime PeriodoDesde,
    DateTime PeriodoHasta,
    string TipoPeriodo,
    decimal SalarioBruto
);

public record EmpleadoDesgloseDecimoItem(
    string Cedula,
    string NombreCompleto,
    List<PeriodoPagoDecimoItem> Periodos,
    decimal TotalDevengado,
    decimal MontoDecimo,
    decimal CssEmpleado,
    decimal SeEmpleado,
    decimal ISR,
    decimal TotalDeducciones,
    decimal NetoPago
);

public record TotalesDesgloseDecimo(
    int TotalEmpleados,
    decimal TotalDevengado,
    decimal TotalDecimo,
    decimal TotalCss,
    decimal TotalSe,
    decimal TotalIsr,
    decimal TotalDeducciones,
    decimal TotalNeto
);
