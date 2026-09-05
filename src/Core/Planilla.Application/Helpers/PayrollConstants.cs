// ====================================================================
// Planilla - PayrollConstants
// Source: Core360 Stage 4
// Portado: 2025-12-26
// Descripción: Constantes de planilla (frecuencias de pago, etc.)
// ====================================================================

using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Helpers;

/// <summary>
/// Constantes utilizadas en cálculos de planilla.
/// </summary>
public static class PayrollConstants
{
    /// <summary>
    /// Frecuencias de pago y su equivalente en períodos por año.
    /// Utilizado para proyectar salarios anuales desde salarios por período.
    /// </summary>
    /// <example>
    /// Salario quincenal B/. 1,000 → Anual = 1,000 * 24 = B/. 24,000
    /// Salario mensual B/. 2,000 → Anual = 2,000 * 12 = B/. 24,000
    /// </example>
    public static readonly Dictionary<string, int> PayFrequencies = new()
    {
        { "Quincenal", 24 },  // 2 pagos por mes × 12 meses
        { "Mensual", 12 },    // 1 pago por mes × 12 meses
        { "Semanal", 52 },    // ~4.33 semanas/mes × 12 meses (aproximadamente)
        { "Bisemanal", 26 }   // 2 pagos por mes (cada 2 semanas exactas)
    };

    /// <summary>
    /// Meses incluyendo décimo tercer mes para proyección anual de ISR.
    /// La DGI permite distribuir el ISR del décimo uniformemente en todos los períodos.
    /// </summary>
    public const int MonthsIncludingDecimo = 13;

    /// <summary>
    /// Periodos EQUIVALENTES de un año para repartir la retencion de ISR.
    ///
    /// El decimo tercer mes tambien tributa, asi que tiene que entrar en el reparto.
    /// Como equivale a un mes de salario, en periodos equivalentes son P/12; el total
    /// es entonces P + P/12 = P x 13/12.
    ///
    ///   Semanal   52 -> 56.33     Quincenal 24 -> 26.00
    ///   Bisemanal 26 -> 28.17     Mensual   12 -> 13.00
    ///
    /// El caso mensual da 13, que es el "x13 meses" que ya se usaba: es el mismo
    /// principio expresado en otras unidades. El quincenal da 26, que es el divisor
    /// que emplea el contador en su libro de retencion.
    ///
    /// Reparte asi: la planilla regular retiene P/(P x 13/12) del impuesto anual y el
    /// modulo de decimo el resto (isrAnual/13 = 1/13 del anual, o 2/26 en quincenal),
    /// de modo que entre ambos suman exactamente el 100%.
    /// </summary>
    public static decimal GetEquivalentPeriodsPerYear(string payFrequency)
        => GetPeriodsPerYear(payFrequency) * MonthsIncludingDecimo / 12m;

    /// <inheritdoc cref="GetEquivalentPeriodsPerYear(string)"/>
    public static decimal GetEquivalentPeriodsPerYear(PayPeriodType periodType)
        => GetPeriodsPerYear(periodType) * MonthsIncludingDecimo / 12m;

    /// <summary>
    /// Periodos que un empleado alcanzara a trabajar dentro del año fiscal, contados desde su
    /// fecha de ingreso. Devuelve null cuando entro antes del año en curso — en ese caso se
    /// proyecta el año completo y no hay nada que prorratear.
    ///
    /// Sirve para no anualizar el salario de quien ingresa a mitad de año como si lo hubiera
    /// cobrado desde enero, que lo empujaria a un tramo de ISR que no le corresponde.
    /// </summary>
    public static decimal? GetRemainingPeriodsInYear(
        DateTime hireDate, DateTime calculationDate, string payFrequency)
    {
        if (hireDate.Year != calculationDate.Year) return null;   // entro en un año anterior

        var periodsPerYear = GetPeriodsPerYear(payFrequency);
        if (periodsPerYear <= 0) return null;

        // Fraccion del año que queda desde el ingreso, contada en dias.
        var finDeAño = new DateTime(hireDate.Year, 12, 31);
        var diasDelAño = DateTime.IsLeapYear(hireDate.Year) ? 366m : 365m;
        var diasRestantes = (decimal)(finDeAño - hireDate.Date).TotalDays + 1m;

        var periodos = periodsPerYear * (diasRestantes / diasDelAño);
        return periodos > 0 ? periodos : null;
    }

    // ====================================================================
    // Tasas CSS / Seguro Educativo — Ley 462
    // ====================================================================

    /// <summary>Tasa CSS empleado: 9.75% del salario base sujeto a CSS.</summary>
    public const decimal CssTasaEmpleado = 0.0975m;

    /// <summary>
    /// Tasa CSS patronal vigente 2026: 13.25% (Ley 51/2005 Art. 96.2.a, modif. Ley 462).
    /// Sube a 14.25% (mar-2027) y 15.25% (mar-2029). El motor de planilla resuelve la tasa
    /// por fecha vía IPayrollConfigProvider; esta constante la consume el cálculo de
    /// liquidaciones para el CSS patronal de vacaciones/décimo del período en curso.
    /// </summary>
    public const decimal CssTasaPatronal = 0.1325m;

    /// <summary>Tasa Seguro Educativo empleado: 1.25%.</summary>
    public const decimal SeTasaEmpleado = 0.0125m;

    /// <summary>Tasa Seguro Educativo patronal: 1.50%.</summary>
    public const decimal SeTasaPatronal = 0.015m;

    /// <summary>
    /// Tasa CSS empleado aplicable al décimo tercer mes: 7.25%.
    /// Base legal: Art. 59, Ley 29 de 1976 (tasa reducida para el décimo).
    /// Distinta a la tasa CSS regular del 9.75%.
    /// </summary>
    public const decimal CssTasaDecimoEmpleado = 0.0725m;

    /// <summary>
    /// Tasa CSS patronal aplicable al décimo tercer mes: 10.75%.
    /// Base legal: Art. 59, Ley 29 de 1976 (tasa reducida para el décimo).
    /// Distinta a la tasa CSS patronal regular del 12.25%.
    /// </summary>
    public const decimal CssTasaDecimoPatronal = 0.1075m;

    // ====================================================================

    /// <summary>
    /// Obtiene el número de períodos por año para una frecuencia de pago dada.
    /// </summary>
    /// <param name="payFrequency">Frecuencia de pago (Quincenal, Mensual, Semanal)</param>
    /// <returns>Número de períodos por año</returns>
    /// <exception cref="ArgumentException">Si la frecuencia no es válida</exception>
    public static int GetPeriodsPerYear(string payFrequency)
    {
        if (!PayFrequencies.TryGetValue(payFrequency, out var periods))
        {
            throw new ArgumentException(
                $"Frecuencia de pago inválida: '{payFrequency}'. " +
                $"Valores permitidos: {string.Join(", ", PayFrequencies.Keys)}",
                nameof(payFrequency));
        }

        return periods;
    }

    /// <summary>
    /// Obtiene períodos/año desde el enum PayPeriodType.
    /// </summary>
    public static int GetPeriodsPerYear(PayPeriodType periodType)
    {
        return periodType switch
        {
            PayPeriodType.Semanal => 52,
            PayPeriodType.Bisemanal => 26,
            PayPeriodType.Quincenal => 24,
            PayPeriodType.Mensual => 12,
            _ => throw new ArgumentException($"Tipo de período inválido: {periodType}")
        };
    }
}
