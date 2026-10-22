namespace Vorluno.Planilla.Application.DTOs.Reportes;

/// <summary>
/// Control interno de horas extra de un mes: qué se registró, qué decidió el
/// director, qué se pagó y cuánto le costó a la empresa. No se envía a nadie;
/// lo oficial (planilla, SIPE, renta) ya incluye lo pagado.
/// </summary>
public record ReporteHorasExtraDto(
    string NombreEmpresa,
    string Ruc,
    string Periodo,
    DateTime FechaGeneracion,
    List<EmpleadoHorasExtraItem> Empleados,
    List<HorasExtraPorTipoItem> PorTipo,
    TotalesHorasExtra Totales,
    decimal TasaCssPatronal,
    decimal TasaSePatronal);

public record EmpleadoHorasExtraItem(
    int EmpleadoId,
    string Cedula,
    string NombreCompleto,
    decimal HorasRegistradas,
    decimal HorasAprobadas,
    decimal HorasRechazadas,
    decimal HorasPendientes,
    decimal MontoAprobado,
    decimal MontoPagado,
    decimal CuotasPatronales,
    List<RegistroHoraExtraItem> Registros);

public record RegistroHoraExtraItem(
    int Id,
    DateTime Fecha,
    string Tipo,
    string Horario,
    decimal Horas,
    decimal Monto,
    // "Pendiente" | "Aprobada" | "Rechazada" | "Pagada"
    string Estado,
    string? DecididoPor,
    string? MotivoRechazo,
    string Motivo);

public record HorasExtraPorTipoItem(string Tipo, decimal HorasAprobadas, decimal MontoAprobado);

public record TotalesHorasExtra(
    decimal HorasRegistradas,
    decimal HorasAprobadas,
    decimal HorasRechazadas,
    decimal HorasPendientes,
    decimal MontoAprobado,
    decimal MontoPagado,
    decimal MontoRechazado,
    decimal CuotasPatronales,
    decimal CostoTotal);
