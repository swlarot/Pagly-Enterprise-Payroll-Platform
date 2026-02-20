// ====================================================================
// Planilla - ReporteComprobantesDto (C-012)
// Comprobantes de pago individuales por empleado (recibos de sueldo)
// ====================================================================

namespace Vorluno.Planilla.Application.DTOs.Reportes;

public record ReporteComprobantesDto(
    string NombreEmpresa, string Ruc,
    string NumeroPlanilla, string Periodo, DateTime FechaPago,
    List<ComprobanteEmpleado> Comprobantes
);

public record ComprobanteEmpleado(
    string Cedula, string NombreCompleto, string? Cargo, string? Departamento,
    decimal SalarioBase, decimal PagoHorasDomingo, decimal PagoHorasFeriado,
    decimal PagoHorasExtra, decimal SalarioBruto,
    decimal CssEmpleado, decimal SeEmpleado, decimal Isr,
    List<LineaDeduccionComprobante> DeduccionesAcreedores,
    decimal TotalDeduccionesAcreedores,
    decimal TotalDeducciones, decimal SalarioNeto,
    decimal ReservaDecimo,
    bool TuvoLimitacion
);

public record LineaDeduccionComprobante(
    string NombreAcreedor, string TipoDeduccion,
    string Descripcion, decimal Monto
);
