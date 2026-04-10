// ====================================================================
// Planilla - IncomeTaxCalculationServicePortable
// Source: Core360 Stage 4, Sección 5
// Portado: 2025-12-26
// Descripción: Servicio de cálculo de Impuesto Sobre la Renta (ISR) de Panamá
// CRÍTICO: Eliminado fallback silencioso de escalas (debe fallar si no hay brackets)
// Cambios vs Core360:
//   - Eliminado método ApplyDefaultTaxBrackets (fallback silencioso)
//   - Agregado IPayrollConfigProvider
//   - Agregado RoundingPolicy
//   - Usa PayrollConstants.GetPeriodsPerYear()
//   - Lanza PayrollConfigurationException si faltan brackets
// ====================================================================

using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Exceptions;
using Vorluno.Planilla.Application.Helpers;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Application.Results;

namespace Vorluno.Planilla.Application.Services;

/// <summary>
/// Servicio de cálculo de Impuesto Sobre la Renta (ISR).
/// Aplica brackets progresivos según regulaciones de la DGI de Panamá.
/// </summary>
public class IncomeTaxCalculationServicePortable
{
    private readonly IPayrollConfigProvider _configProvider;

    public IncomeTaxCalculationServicePortable(IPayrollConfigProvider configProvider)
    {
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
    }

    /// <summary>
    /// Calcula el Impuesto Sobre la Renta (retención del período).
    /// </summary>
    /// <param name="companyId">ID de compañía</param>
    /// <param name="grossPay">Salario bruto del período</param>
    /// <param name="payFrequency">Frecuencia de pago (Mensual, Quincenal, Semanal)</param>
    /// <param name="dependents">Número de dependientes declarados</param>
    /// <param name="isSubjectToIncomeTax">Indica si el empleado está sujeto a ISR</param>
    /// <param name="calculationDate">Fecha de cálculo (para determinar año fiscal)</param>
    /// <returns>Resultado del cálculo ISR</returns>
    public async Task<IncomeTaxResult> CalculateIncomeTaxAsync(
        int companyId,
        decimal grossPay,
        string payFrequency,
        int dependents,
        bool isSubjectToIncomeTax,
        DateTime calculationDate)
    {
        // Si no está sujeto a ISR, retorna ceros
        if (!isSubjectToIncomeTax)
        {
            return new IncomeTaxResult(
                TaxableIncome: 0,
                DependentDeduction: 0,
                NetTaxableIncome: 0,
                TaxAmount: 0,
                EffectiveTaxRate: 0
            );
        }

        var year = calculationDate.Year;

        // 1. Proyectar ingreso anual basado en la frecuencia de pago
        var annualIncome = ProjectAnnualIncome(grossPay, payFrequency);

        // 2. Calcular deducción por dependientes
        var dependentDeduction = await CalculateDependentDeductionAsync(
            companyId, dependents, calculationDate);

        // 3. Calcular ingreso neto gravable (después de deducciones)
        var netTaxableIncome = Math.Max(0, annualIncome - dependentDeduction);

        // 4. Aplicar brackets progresivos de ISR
        var annualTax = await ApplyTaxBracketsAsync(companyId, netTaxableIncome, year);

        // 5. Convertir impuesto anual a retención del período
        var periodsPerYear = PayrollConstants.GetPeriodsPerYear(payFrequency);
        var periodTax = RoundingPolicy.Round(annualTax / periodsPerYear, 2);

        // 6. Calcular tasa efectiva de impuesto
        var effectiveTaxRate = annualIncome > 0
            ? RoundingPolicy.Round((annualTax / annualIncome) * 100, 2)
            : 0;

        return new IncomeTaxResult(
            TaxableIncome: annualIncome,
            DependentDeduction: dependentDeduction,
            NetTaxableIncome: netTaxableIncome,
            TaxAmount: periodTax,
            EffectiveTaxRate: effectiveTaxRate
        );
    }

    /// <summary>
    /// Proyecta el ingreso anual basado en el salario del período y la frecuencia de pago.
    /// Incluye décimo tercer mes en la proyección (×13 meses) para distribuir
    /// uniformemente la retención de ISR del décimo en todos los períodos de pago.
    /// </summary>
    /// <param name="periodIncome">Salario del período</param>
    /// <param name="payFrequency">Frecuencia de pago (Mensual, Quincenal, Semanal)</param>
    /// <returns>Ingreso anual proyectado incluyendo décimo tercer mes</returns>
    private decimal ProjectAnnualIncome(decimal periodIncome, string payFrequency)
    {
        var periodsPerYear = PayrollConstants.GetPeriodsPerYear(payFrequency);
        // Convertir salario del período a mensual, luego proyectar a 13 meses (12 + décimo)
        var monthlySalary = periodIncome * periodsPerYear / 12m;
        return monthlySalary * PayrollConstants.MonthsIncludingDecimo;
    }

    /// <summary>
    /// Calcula la deducción total por dependientes.
    /// Limita el número de dependientes al máximo permitido por configuración.
    /// </summary>
    /// <param name="companyId">ID de compañía</param>
    /// <param name="dependents">Número de dependientes declarados</param>
    /// <param name="calculationDate">Fecha de cálculo</param>
    /// <returns>Deducción total por dependientes</returns>
    private async Task<decimal> CalculateDependentDeductionAsync(
        int companyId,
        int dependents,
        DateTime calculationDate)
    {
        // Obtener configuración de deducciones
        var config = await _configProvider.GetTaxConfigAsync(companyId, calculationDate);
        if (config == null)
        {
            throw new InvalidOperationException(
                $"No se encontró configuración de ISR para companyId={companyId} en fecha {calculationDate:yyyy-MM-dd}");
        }

        // Limitar dependientes al máximo permitido
        var validDependents = Math.Min(dependents, config.MaxDependents);

        // Calcular deducción total
        var deduction = validDependents * config.DependentDeductionAmount;

        return deduction;
    }

    /// <summary>
    /// Aplica los brackets progresivos de ISR para calcular el impuesto anual.
    /// CRÍTICO: NO hay fallback silencioso. Si no existen brackets, lanza excepción.
    /// </summary>
    /// <param name="companyId">ID de compañía</param>
    /// <param name="taxableIncome">Ingreso neto gravable anual</param>
    /// <param name="year">Año fiscal</param>
    /// <returns>Impuesto anual calculado</returns>
    private async Task<decimal> ApplyTaxBracketsAsync(
        int companyId,
        decimal taxableIncome,
        int year)
    {
        var brackets = await _configProvider.GetTaxBracketsAsync(companyId, year);

        if (brackets == null || brackets.Count == 0)
        {
            throw new PayrollConfigurationException(
                $"No existen tramos de ISR configurados para el año {year} y companyId={companyId}. " +
                "Configure los tramos en la tabla TaxBrackets antes de calcular la planilla.");
        }

        var orderedBrackets = brackets.OrderBy(b => b.MinIncome).ToList();

        // Encontrar el tramo aplicable (el último donde MinIncome < taxableIncome)
        TaxBracketDto? applicableBracket = null;
        foreach (var bracket in orderedBrackets)
        {
            if (taxableIncome > bracket.MinIncome)
            {
                applicableBracket = bracket;
            }
            else
            {
                break;
            }
        }

        if (applicableBracket == null)
            return 0m;

        // ISR = FixedAmount (acumulado de tramos anteriores) + excedente × tasa del tramo
        var excess = taxableIncome - applicableBracket.MinIncome;
        var bracketTax = RoundingPolicy.CalculatePercentage(excess, applicableBracket.Rate);
        var totalTax = applicableBracket.FixedAmount + bracketTax;

        return RoundingPolicy.Round(totalTax, 2);
    }
}
