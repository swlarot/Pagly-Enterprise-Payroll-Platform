// ====================================================================
// Planilla - SalaryHistoryAverager
// Creado: 2026-06-23 (DEV-215 B5) — Calcula las bases salariales de
//   liquidación a partir del historial de salarios del empleado.
//
// La ley pide bases distintas por partida:
//   • Prima de antigüedad (Art. 226): promedio de los últimos 5 años.
//   • Indemnización (Art. 149): promedio de los últimos 6 meses o de los
//     últimos 30 días, lo que resulte MÁS FAVORABLE (mayor) al trabajador.
//
// El promedio es PONDERADO POR TIEMPO: cada salario pesa según los días
// que estuvo vigente dentro de la ventana. Si no hay historial suficiente,
// se hace fallback al salario actual (comportamiento previo a B5).
// ====================================================================

using Vorluno.Planilla.Application.Helpers;
using Vorluno.Planilla.Domain.Entities;

namespace Vorluno.Planilla.Application.Services;

/// <summary>
/// Calcula promedios salariales ponderados por tiempo a partir del historial
/// salarial de un empleado, para las bases de prima e indemnización.
/// </summary>
public static class SalaryHistoryAverager
{
    /// <summary>
    /// Base salarial para la PRIMA de antigüedad (Art. 226): promedio ponderado
    /// de los últimos 5 años (o del tiempo trabajado, si es menor).
    /// </summary>
    public static decimal AverageForPrima(
        IEnumerable<HistorialSalarial> historial, DateTime terminacion, decimal salarioActual)
    {
        var windowStart = terminacion.AddYears(-5);
        return TimeWeightedAverage(historial, windowStart, terminacion, salarioActual);
    }

    /// <summary>
    /// Base salarial para la INDEMNIZACIÓN (Art. 149): el mayor entre el promedio
    /// de los últimos 6 meses y el de los últimos 30 días (lo más favorable).
    /// </summary>
    public static decimal AverageForIndemnization(
        IEnumerable<HistorialSalarial> historial, DateTime terminacion, decimal salarioActual)
    {
        var lista = historial as ICollection<HistorialSalarial> ?? historial.ToList();
        var avg6m = TimeWeightedAverage(lista, terminacion.AddMonths(-6), terminacion, salarioActual);
        var avg30d = TimeWeightedAverage(lista, terminacion.AddDays(-30), terminacion, salarioActual);
        return Math.Max(avg6m, avg30d);
    }

    /// <summary>
    /// Promedio salarial ponderado por los días que cada salario estuvo vigente
    /// dentro de la ventana [windowStart, windowEnd]. Devuelve <paramref name="fallback"/>
    /// si no hay historial que cubra la ventana.
    /// </summary>
    public static decimal TimeWeightedAverage(
        IEnumerable<HistorialSalarial> historial, DateTime windowStart, DateTime windowEnd, decimal fallback)
    {
        if (windowEnd <= windowStart) return fallback;

        // Ordenar por fecha de vigencia ascendente; ignorar registros futuros a la ventana.
        var registros = historial
            .Where(h => h.FechaVigencia < windowEnd)
            .OrderBy(h => h.FechaVigencia)
            .ToList();

        if (registros.Count == 0) return fallback;

        decimal sumaPonderada = 0m;
        decimal diasCubiertos = 0m;

        for (int i = 0; i < registros.Count; i++)
        {
            var segStart = registros[i].FechaVigencia;
            // El salario rige hasta el próximo cambio, o hasta el fin de la ventana.
            var segEnd = i < registros.Count - 1 ? registros[i + 1].FechaVigencia : windowEnd;

            // Solapamiento del segmento con la ventana.
            var overlapStart = segStart > windowStart ? segStart : windowStart;
            var overlapEnd = segEnd < windowEnd ? segEnd : windowEnd;

            var dias = (decimal)(overlapEnd - overlapStart).TotalDays;
            if (dias <= 0) continue;

            sumaPonderada += registros[i].SalarioMensual * dias;
            diasCubiertos += dias;
        }

        if (diasCubiertos <= 0) return fallback;

        return RoundingPolicy.Round(sumaPonderada / diasCubiertos);
    }
}
