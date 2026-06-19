// ====================================================================
// Planilla - IncomeTaxResult
// Source: Core360 Stage 4
// Portado: 2025-12-26
// Descripción: Resultado de cálculo de Impuesto Sobre la Renta (ISR)
// ====================================================================

namespace Vorluno.Planilla.Application.Results;

/// <summary>
/// Resultado del cálculo de Impuesto Sobre la Renta (ISR).
/// El ISR se calcula sobre proyección anual con brackets progresivos.
/// </summary>
/// <param name="TaxableIncome">Ingreso anual proyectado</param>
/// <param name="DependentDeduction">Deducción por dependientes (Art. 709 núm. 3)</param>
/// <param name="SeDeduction">Deducción por contribución al Seguro Educativo del empleado (Art. 709 núm. 4)</param>
/// <param name="NetTaxableIncome">Ingreso neto gravable (TaxableIncome - DependentDeduction - SeDeduction)</param>
/// <param name="TaxAmount">Impuesto del período (anual / períodos por año)</param>
/// <param name="EffectiveTaxRate">Tasa efectiva de impuesto (TaxAmount / TaxableIncome × 100)</param>
public record IncomeTaxResult(
    decimal TaxableIncome,
    decimal DependentDeduction,
    decimal SeDeduction,
    decimal NetTaxableIncome,
    decimal TaxAmount,
    decimal EffectiveTaxRate
);
