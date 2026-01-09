// ====================================================================
// Planilla - RoundingPolicy Helper
// Source: Core360 Stage 4, Planilla Decision
// Portado: 2025-12-26
// Descripción: Política de redondeo para cálculos monetarios de planilla
// Decisión Planilla: MidpointRounding.AwayFromZero (redondea 0.5 hacia arriba)
// ====================================================================

namespace Vorluno.Planilla.Application.Helpers;

/// <summary>
/// Política de redondeo centralizada para cálculos de planilla.
/// Garantiza consistencia en redondeo de montos y porcentajes.
/// </summary>
public static class RoundingPolicy
{
    /// <summary>
    /// Redondea un valor decimal a la cantidad de decimales especificada.
    /// Usa MidpointRounding.AwayFromZero (ej: 0.5 → 1, -0.5 → -1)
    /// </summary>
    /// <param name="value">Valor a redondear</param>
    /// <param name="decimals">Cantidad de decimales (por defecto 2 para moneda)</param>
    /// <returns>Valor redondeado</returns>
    /// <example>
    /// Round(146.125, 2) → 146.13
    /// Round(146.124, 2) → 146.12
    /// </example>
    public static decimal Round(decimal value, int decimals = 2)
    {
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Calcula un porcentaje sobre un monto base y redondea el resultado.
    /// Fórmula: baseAmount * rate / 100, redondeado a 2 decimales
    /// </summary>
    /// <param name="baseAmount">Monto base sobre el cual aplicar el porcentaje</param>
    /// <param name="rate">Tasa porcentual (ej: 9.75 para 9.75%)</param>
    /// <returns>Resultado del cálculo redondeado a 2 decimales</returns>
    /// <example>
    /// CalculatePercentage(1500, 9.75) → 146.25
    /// CalculatePercentage(1000, 13.25) → 132.50
    /// </example>
    public static decimal CalculatePercentage(decimal baseAmount, decimal rate)
    {
        return Round(baseAmount * rate / 100, 2);
    }
}
