// ====================================================================
// Planilla - IDevengadoMensualService
// La única fuente de "cuánto devengó este empleado en tal mes".
// ====================================================================

using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>Un mes de un empleado, venga de donde venga.</summary>
public sealed record MesDevengado(
    int Anio,
    int Mes,
    decimal Salario,
    decimal Vacaciones,
    decimal Extras,
    decimal Comision,
    OrigenDevengado Origen)
{
    public decimal Total => Salario + Vacaciones + Extras + Comision;
    public bool TieneDatos => Origen != OrigenDevengado.SinDatos;
}

/// <summary>
/// Devuelve los meses devengados de un empleado combinando dos fuentes, en
/// este orden de prioridad:
///   1. Planillas aprobadas o pagadas de Pagly (por el mes de su período trabajado).
///   2. Registros DevengadoMensual (importados al migrar o escritos a mano).
/// Un mes sin ninguna de las dos se devuelve igual, con Origen = SinDatos y
/// ceros, para que quien calcule sepa qué le falta y lo avise.
/// </summary>
public interface IDevengadoMensualService
{
    /// <summary>Todos los meses entre dos fechas (inclusive), uno por mes, ordenados.</summary>
    Task<IReadOnlyList<MesDevengado>> ObtenerMesesAsync(
        int empleadoId, DateTime desde, DateTime hasta, CancellationToken ct = default);

    /// <summary>Los N meses completos anteriores a una fecha, terminando en el mes de esa fecha.</summary>
    Task<IReadOnlyList<MesDevengado>> UltimosMesesAsync(
        int empleadoId, DateTime hasta, int cantidad, CancellationToken ct = default);

    /// <summary>
    /// Guarda o actualiza un mes importado/manual. Rechaza el mes si ya tiene
    /// planilla aprobada en Pagly: ese mes se deriva y no se sobreescribe.
    /// </summary>
    Task GuardarMesAsync(
        int empleadoId, int anio, int mes,
        decimal salario, decimal vacaciones, decimal extras, decimal comision,
        OrigenDevengado origen, string? nota = null, CancellationToken ct = default);

    /// <summary>true si ese mes tiene al menos una planilla aprobada o pagada.</summary>
    Task<bool> MesTienePlanillaAsync(int empleadoId, int anio, int mes, CancellationToken ct = default);
}
