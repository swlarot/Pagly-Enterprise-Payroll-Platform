// ====================================================================
// Planilla - OvertimeClassifier
// Creado: 2026-06-18 — Lógica pura de clasificación de horas extra y factores
//   (Código de Trabajo Panamá, Arts. 30, 33, 36, 48-50). Extraída de
//   OvertimeFactorService (Infrastructure) para ser testeable sin DbContext.
// ====================================================================

using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Services;

/// <summary>
/// Clasificador puro de jornadas para horas extra y factores multiplicadores.
/// Sin dependencias de infraestructura — testeable directamente.
/// </summary>
public static class OvertimeClassifier
{
    // Art. 30: período diurno 6:00am–6:00pm; nocturno 6:00pm–6:00am.
    private const double DayStartMin = 6 * 60;
    private const double DayEndMin = 18 * 60;
    // Art. 30 párrafo 2: una jornada con más de 3 horas en período nocturno es NOCTURNA.
    private const double NocturnalThresholdHours = 3.0;

    /// <summary>
    /// Factor multiplicador base de cada tipo de hora extra (Arts. 33, 36, 48-50).
    /// El factor incluye el 100% del salario + el recargo legal.
    /// </summary>
    public static decimal FactorBase(TipoHoraExtra tipo)
    {
        return tipo switch
        {
            TipoHoraExtra.Diurna => 1.25m,                  // Art. 33.1
            TipoHoraExtra.Nocturna => 1.50m,                // Art. 33.2
            TipoHoraExtra.MixtaDiurnaNocturna => 1.50m,     // Art. 33.2
            TipoHoraExtra.MixtaNocturnaDiurna => 1.75m,     // Art. 33.3
            TipoHoraExtra.DomingoFeriado => 1.50m,          // Art. 48 — jornada ordinaria dominical
            TipoHoraExtra.DominicalHEDiurna => 1.875m,      // Arts. 48 + 50 — 1.50 × 1.25
            TipoHoraExtra.NocturnaDomingoFeriado => 2.25m,  // Arts. 48 + 50 — 1.50 × 1.50
            TipoHoraExtra.FeriadoOrdinario => 2.50m,        // Art. 49 — jornada ordinaria en feriado
            TipoHoraExtra.DiaSustituto => 1.50m,            // Art. 49 inciso 2
            TipoHoraExtra.FiestaNacionalDiurna => 3.125m,   // Arts. 49 + 50 — 2.50 × 1.25
            TipoHoraExtra.FiestaNacionalNocturna => 3.75m,  // Arts. 49 + 50 — 2.50 × 1.50
            _ => 1.25m
        };
    }

    /// <summary>
    /// Recargo adicional legal por exceder los límites de jornada extraordinaria
    /// (más de 3h/día o 9h/semana). Código de Trabajo Art. 36.4.
    /// </summary>
    public const decimal FactorExcesoLegal = 1.75m;

    /// <summary>Todos los tipos de hora extra del catálogo, en orden de presentación.</summary>
    public static readonly IReadOnlyList<TipoHoraExtra> TodosLosTipos = new[]
    {
        TipoHoraExtra.Diurna,
        TipoHoraExtra.Nocturna,
        TipoHoraExtra.MixtaDiurnaNocturna,
        TipoHoraExtra.MixtaNocturnaDiurna,
        TipoHoraExtra.DomingoFeriado,
        TipoHoraExtra.DominicalHEDiurna,
        TipoHoraExtra.NocturnaDomingoFeriado,
        TipoHoraExtra.FeriadoOrdinario,
        TipoHoraExtra.FiestaNacionalDiurna,
        TipoHoraExtra.FiestaNacionalNocturna,
        TipoHoraExtra.DiaSustituto
    };

    /// <summary>Nombre legible de cada tipo, para la UI de Configuración.</summary>
    public static string Nombre(TipoHoraExtra tipo) => tipo switch
    {
        TipoHoraExtra.Diurna => "Hora extra diurna",
        TipoHoraExtra.Nocturna => "Hora extra nocturna",
        TipoHoraExtra.MixtaDiurnaNocturna => "Mixta (inicia diurna)",
        TipoHoraExtra.MixtaNocturnaDiurna => "Mixta (inicia nocturna)",
        TipoHoraExtra.DomingoFeriado => "Domingo — jornada ordinaria",
        TipoHoraExtra.DominicalHEDiurna => "Domingo — hora extra diurna",
        TipoHoraExtra.NocturnaDomingoFeriado => "Domingo — hora extra nocturna",
        TipoHoraExtra.FeriadoOrdinario => "Feriado — jornada ordinaria",
        TipoHoraExtra.FiestaNacionalDiurna => "Feriado — hora extra diurna",
        TipoHoraExtra.FiestaNacionalNocturna => "Feriado — hora extra nocturna",
        TipoHoraExtra.DiaSustituto => "Día sustituto tras feriado",
        _ => tipo.ToString()
    };

    /// <summary>Artículo del Código de Trabajo que respalda el factor legal.</summary>
    public static string BaseLegal(TipoHoraExtra tipo) => tipo switch
    {
        TipoHoraExtra.Diurna => "Art. 33.1",
        TipoHoraExtra.Nocturna => "Art. 33.2",
        TipoHoraExtra.MixtaDiurnaNocturna => "Art. 33.2",
        TipoHoraExtra.MixtaNocturnaDiurna => "Art. 33.3",
        TipoHoraExtra.DomingoFeriado => "Art. 48",
        TipoHoraExtra.DominicalHEDiurna => "Arts. 48 + 50",
        TipoHoraExtra.NocturnaDomingoFeriado => "Arts. 48 + 50",
        TipoHoraExtra.FeriadoOrdinario => "Art. 49",
        TipoHoraExtra.FiestaNacionalDiurna => "Arts. 49 + 50",
        TipoHoraExtra.FiestaNacionalNocturna => "Arts. 49 + 50",
        TipoHoraExtra.DiaSustituto => "Art. 49 inciso 2",
        _ => "—"
    };

    /// <summary>
    /// ¿La jornada se considera nocturna? Es nocturna si toda cae en período nocturno
    /// o si comprende más de 3 horas dentro de él (Art. 30).
    /// </summary>
    public static bool EsNocturnaPorHorario(TimeSpan horaInicio, TimeSpan horaFin)
    {
        var horasNocturnas = HorasEnPeriodoNocturno(horaInicio, horaFin);
        var horasDiurnas = HorasTotales(horaInicio, horaFin) - horasNocturnas;
        return horasDiurnas <= 0 || horasNocturnas > NocturnalThresholdHours;
    }

    /// <summary>
    /// Clasifica una jornada de día regular (no feriado, no domingo) según el horario,
    /// aplicando la regla del Art. 30: más de 3 horas en período nocturno ⇒ NOCTURNA.
    /// </summary>
    public static TipoHoraExtra ClasificarJornadaRegular(TimeSpan horaInicio, TimeSpan horaFin)
    {
        var horasNocturnas = HorasEnPeriodoNocturno(horaInicio, horaFin);
        var horasTotales = HorasTotales(horaInicio, horaFin);
        var horasDiurnas = horasTotales - horasNocturnas;

        if (horasNocturnas <= 0) return TipoHoraExtra.Diurna;
        if (horasDiurnas <= 0) return TipoHoraExtra.Nocturna;
        if (horasNocturnas > NocturnalThresholdHours) return TipoHoraExtra.Nocturna; // Art. 30 párr. 2

        var inicioMin = horaInicio.TotalMinutes;
        bool iniciaDiurno = inicioMin >= DayStartMin && inicioMin < DayEndMin;
        return iniciaDiurno ? TipoHoraExtra.MixtaDiurnaNocturna : TipoHoraExtra.MixtaNocturnaDiurna;
    }

    /// <summary>Total de horas del intervalo [inicio, fin], manejando el cruce de medianoche.</summary>
    public static double HorasTotales(TimeSpan horaInicio, TimeSpan horaFin)
    {
        var startMin = horaInicio.TotalMinutes;
        var endMin = horaFin.TotalMinutes;
        if (endMin <= startMin) endMin += 24 * 60;
        return (endMin - startMin) / 60.0;
    }

    /// <summary>
    /// Cuenta las horas del intervalo [inicio, fin] dentro del período nocturno
    /// (6:00pm–6:00am, Art. 30), manejando el cruce de medianoche por solapamiento de rangos.
    /// </summary>
    public static double HorasEnPeriodoNocturno(TimeSpan horaInicio, TimeSpan horaFin)
    {
        var startMin = horaInicio.TotalMinutes;
        var endMin = horaFin.TotalMinutes;
        if (endMin <= startMin) endMin += 24 * 60;

        (double Start, double End)[] rangosNocturnos =
        {
            (0, 6 * 60),
            (18 * 60, 24 * 60),
            (24 * 60, 30 * 60),
            (42 * 60, 48 * 60)
        };

        double nocturnas = 0;
        foreach (var (rs, re) in rangosNocturnos)
        {
            var overlapStart = Math.Max(startMin, rs);
            var overlapEnd = Math.Min(endMin, re);
            if (overlapEnd > overlapStart) nocturnas += overlapEnd - overlapStart;
        }
        return nocturnas / 60.0;
    }
}
