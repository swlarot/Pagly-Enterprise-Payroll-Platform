namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// DTO con estadísticas agregadas de horas extra
/// </summary>
public record HorasExtraEstadisticasDto
{
    /// <summary>Total de horas extra diurnas</summary>
    public decimal TotalHorasDiurnas { get; init; }

    /// <summary>Total de horas extra nocturnas</summary>
    public decimal TotalHorasNocturnas { get; init; }

    /// <summary>Total de horas extra en domingo/feriado</summary>
    public decimal TotalHorasDomingoFeriado { get; init; }

    /// <summary>Total de horas extra en festivos nacionales</summary>
    public decimal TotalHorasFestivos { get; init; }

    /// <summary>Total de horas extra mixtas</summary>
    public decimal TotalHorasMixtas { get; init; }

    /// <summary>Total de horas con exceso</summary>
    public decimal TotalHorasExceso { get; init; }

    /// <summary>Total de horas extra (todas)</summary>
    public decimal TotalHoras { get; init; }

    /// <summary>Monto total por horas extra diurnas</summary>
    public decimal MontoDiurnas { get; init; }

    /// <summary>Monto total por horas extra nocturnas</summary>
    public decimal MontoNocturnas { get; init; }

    /// <summary>Monto total por horas extra en domingo/feriado</summary>
    public decimal MontoDomingoFeriado { get; init; }

    /// <summary>Monto total por horas extra en festivos</summary>
    public decimal MontoFestivos { get; init; }

    /// <summary>Monto total por horas extra mixtas</summary>
    public decimal MontoMixtas { get; init; }

    /// <summary>Monto total por horas con exceso</summary>
    public decimal MontoExceso { get; init; }

    /// <summary>Monto total (todas las horas extra)</summary>
    public decimal MontoTotal { get; init; }

    /// <summary>Promedio de horas extra por semana</summary>
    public decimal PromedioHorasPorSemana { get; init; }

    /// <summary>Promedio de horas extra por mes</summary>
    public decimal PromedioHorasPorMes { get; init; }

    /// <summary>Lista de fechas de días festivos trabajados</summary>
    public List<DateTime> DiasFestivosTrabajados { get; init; } = new();

    /// <summary>Total de horas con exceso</summary>
    public decimal TotalHorasConExceso { get; init; }

    /// <summary>Porcentaje de horas con exceso sobre el total</summary>
    public decimal PorcentajeHorasExceso { get; init; }

    /// <summary>Comparación con período anterior (si aplica)</summary>
    public ComparacionPeriodoDto? ComparacionPeriodoAnterior { get; init; }
}

/// <summary>
/// Comparación con período anterior
/// </summary>
public record ComparacionPeriodoDto
{
    /// <summary>Total de horas en período anterior</summary>
    public decimal TotalHorasAnterior { get; init; }

    /// <summary>Monto total en período anterior</summary>
    public decimal MontoTotalAnterior { get; init; }

    /// <summary>Diferencia de horas (actual - anterior)</summary>
    public decimal DiferenciaHoras { get; init; }

    /// <summary>Diferencia de monto (actual - anterior)</summary>
    public decimal DiferenciaMonto { get; init; }

    /// <summary>Porcentaje de cambio en horas</summary>
    public decimal PorcentajeCambioHoras { get; init; }

    /// <summary>Porcentaje de cambio en monto</summary>
    public decimal PorcentajeCambioMonto { get; init; }
}
