namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Categoria de prelacion legal de una deduccion.
/// Determina el orden de aplicacion y el comportamiento respecto al salario minimo.
/// </summary>
public enum CategoriaDeduccion
{
    /// <summary>
    /// Pension alimenticia - Prioridad absoluta despues de legales.
    /// PUEDE reducir bajo salario minimo por orden judicial.
    /// </summary>
    PensionAlimenticia = 1,

    /// <summary>
    /// Embargo judicial - Solo afecta excedente sobre salario minimo.
    /// </summary>
    EmbargoJudicial = 2,

    /// <summary>
    /// Deduccion voluntaria - No puede reducir bajo salario minimo.
    /// Incluye prestamos, sindicato, seguro medico, cooperativas, etc.
    /// </summary>
    Voluntaria = 3
}
