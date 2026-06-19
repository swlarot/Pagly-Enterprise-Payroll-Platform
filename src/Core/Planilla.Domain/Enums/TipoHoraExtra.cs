namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Tipos de horas extra según el Código de Trabajo de Panamá
/// </summary>
public enum TipoHoraExtra
{
    /// <summary>
    /// Hora extra diurna - Factor 1.25x (6am-6pm días normales)
    /// </summary>
    Diurna = 1,

    /// <summary>
    /// Hora extra nocturna - Factor 1.50x (6pm-6am)
    /// </summary>
    Nocturna = 2,

    /// <summary>
    /// Hora extra en domingo o feriado (diurna) - Factor 1.50x
    /// </summary>
    DomingoFeriado = 3,

    /// <summary>
    /// Hora extra nocturna en domingo o feriado - Factor 2.25x (1.50 × 1.50)
    /// Según Código de Trabajo Art. 50: primero recargo dominical (1.50x), luego nocturno (1.50x)
    /// </summary>
    NocturnaDomingoFeriado = 4,

    /// <summary>
    /// Hora extra en día de fiesta nacional o duelo nacional (diurna) - Factor 3.125x (2.50 × 1.25)
    /// Según Código de Trabajo Art. 49: recargo 150% (2.50x) sobre jornada ordinaria diurna (1.25x)
    /// </summary>
    FiestaNacionalDiurna = 5,

    /// <summary>
    /// Hora extra nocturna en día de fiesta nacional - Factor 3.75x (2.50 × 1.50)
    /// Según Código de Trabajo Art. 49: recargo 150% (2.50x) sobre jornada ordinaria nocturna (1.50x)
    /// </summary>
    FiestaNacionalNocturna = 6,

    /// <summary>
    /// Hora extra mixta diurna-nocturna - Factor 1.50x (inicia en horario diurno)
    /// Según Código de Trabajo Art. 33: jornada mixta que inicia en horario diurno
    /// </summary>
    MixtaDiurnaNocturna = 7,

    /// <summary>
    /// Hora extra mixta nocturna-diurna - Factor 1.75x (inicia en horario nocturno)
    /// Según Código de Trabajo Art. 33: jornada mixta que inicia en horario nocturno
    /// </summary>
    MixtaNocturnaDiurna = 8,

    /// <summary>
    /// Hora extra diurna en domingo - Factor 1.875x (1.50 dominical × 1.25 HE diurna).
    /// Código de Trabajo Arts. 48 + 50.
    /// </summary>
    DominicalHEDiurna = 9,

    /// <summary>
    /// Jornada ordinaria en día feriado o de duelo nacional - Factor 2.50x.
    /// Código de Trabajo Art. 49 (distinto de la HE en feriado, que es 3.125x/3.75x).
    /// </summary>
    FeriadoOrdinario = 10,

    /// <summary>
    /// Día sustituto trabajado tras un feriado - Factor 1.50x.
    /// Código de Trabajo Art. 49 inciso 2.
    /// </summary>
    DiaSustituto = 11
}
