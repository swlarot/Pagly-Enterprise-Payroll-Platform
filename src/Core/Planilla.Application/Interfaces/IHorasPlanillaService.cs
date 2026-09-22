// ====================================================================
// Planilla - IHorasPlanillaService
// Las horas de una planilla se preparan solas al crearla: una fila por
// empleado activo con sus horas regulares, más las horas extra y ausencias
// aprobadas del período. Antes eran dos botones ("Auto-llenar regulares" e
// "Importar novedades"); ahora es parte de crear la planilla.
// ====================================================================

using Vorluno.Planilla.Domain.Entities;

namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>Cómo tratar horas extra/ausencias ya escritas en la planilla.</summary>
public enum ModoNovedades
{
    /// <summary>Si algún empleado ya tiene novedades, no escribe y pide confirmación.</summary>
    Preguntar,
    /// <summary>Reemplaza lo que hubiera por lo aprobado.</summary>
    Sobrescribir,
    /// <summary>Suma lo aprobado a lo que hubiera.</summary>
    Sumar,
}

public sealed record ResumenNovedades(
    bool RequiereConfirmacion,
    int EmpleadosConValoresPrevios,
    int EmpleadosConNovedades,
    decimal HorasExtraDiurnas,
    decimal HorasExtraNocturnas,
    decimal HorasAusencia)
{
    public decimal HorasExtra => HorasExtraDiurnas + HorasExtraNocturnas;
}

public interface IHorasPlanillaService
{
    /// <summary>
    /// Crea la fila de horas (con las regulares del empleado) para cada empleado
    /// activo que todavía no la tenga. No guarda: el llamador decide cuándo.
    /// Devuelve cuántas filas se agregaron.
    /// </summary>
    Task<int> GenerarHorasPorDefectoAsync(PayrollHeader planilla, CancellationToken ct = default);

    /// <summary>
    /// Lleva a las filas de horas las horas extra aprobadas y las ausencias que
    /// afectan salario dentro del período, en dos consultas (no una por empleado).
    /// No guarda. Si el modo es Preguntar y hay valores previos, no toca nada y
    /// devuelve RequiereConfirmacion = true.
    /// </summary>
    Task<ResumenNovedades> ImportarNovedadesAsync(PayrollHeader planilla, ModoNovedades modo, CancellationToken ct = default);
}
