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

    // ====================================================================
    // Tasas CSS / Seguro Educativo — Ley 462
    // ====================================================================

    /// <summary>Tasa CSS empleado: 9.75% del salario base sujeto a CSS.</summary>
    public const decimal CssTasaEmpleado = 0.0975m;

    /// <summary>Tasa CSS patronal: 12.25% del salario base sujeto a CSS.</summary>
    public const decimal CssTasaPatronal = 0.1225m;

    /// <summary>Tasa Seguro Educativo empleado: 1.25%.</summary>
    public const decimal SeTasaEmpleado = 0.0125m;

    /// <summary>Tasa Seguro Educativo patronal: 1.50%.</summary>
    public const decimal SeTasaPatronal = 0.015m;

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
