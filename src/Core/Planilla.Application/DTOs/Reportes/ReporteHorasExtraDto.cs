namespace Vorluno.Planilla.Application.DTOs.Reportes;

/// <summary>
/// DTO para reporte detallado de horas extra de una planilla
/// </summary>
public record ReporteHorasExtraDto(
    string NombreEmpresa,
    string RucEmpresa,
    string Periodo,
    DateTime FechaGeneracion,
    List<EmpleadoHorasExtraDto> Empleados,
    TotalesHorasExtraDto Totales
);

/// <summary>
/// DTO para horas extra de un empleado específico
/// </summary>
public record EmpleadoHorasExtraDto(
    string NumeroIdentificacion,
    string NombreCompleto,
    decimal HorasDiurnas,
    decimal HorasNocturnas,
    decimal HorasDomingoFeriado,
    decimal HorasFestivos,
    decimal HorasMixtas,
    decimal HorasExceso,
    decimal TotalHoras,
    decimal MontoDiurnas,
    decimal MontoNocturnas,
    decimal MontoDomingoFeriado,
    decimal MontoFestivos,
    decimal MontoMixtas,
    decimal MontoExceso,
    decimal MontoTotal
);

/// <summary>
/// Totales del reporte de horas extra
/// </summary>
public record TotalesHorasExtraDto(
    decimal TotalHorasDiurnas,
    decimal TotalHorasNocturnas,
    decimal TotalHorasDomingoFeriado,
    decimal TotalHorasFestivos,
    decimal TotalHorasMixtas,
    decimal TotalHorasExceso,
    decimal TotalHoras,
    decimal TotalMontoDiurnas,
    decimal TotalMontoNocturnas,
    decimal TotalMontoDomingoFeriado,
    decimal TotalMontoFestivos,
    decimal TotalMontoMixtas,
    decimal TotalMontoExceso,
    decimal TotalMonto
);
