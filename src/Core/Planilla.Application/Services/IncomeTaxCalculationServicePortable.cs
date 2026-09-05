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
    /// <param name="dependents">
    /// Número de dependientes declarados. Se conserva en la firma por compatibilidad y para
    /// reportes, pero YA NO afecta la retención: la deducción de B/. 800 es por pareja en
    /// declaración conjunta y se ajusta en la declaración anual, no en planilla.
    /// </param>
    /// <param name="isSubjectToIncomeTax">Indica si el empleado está sujeto a ISR</param>
    /// <param name="isSubjectToEducationalInsurance">
    /// Indica si el empleado cotiza Seguro Educativo. Su contribución (1.25%) es deducible
    /// de la base imponible del ISR según el Art. 709 numeral 4 del Código Fiscal.
    /// </param>
    /// <param name="calculationDate">Fecha de cálculo (para determinar año fiscal)</param>
    /// <param name="variablePay">
    /// Parte VARIABLE del bruto del período: horas extra, comisiones, bonificaciones.
    /// No se proyecta al año — se grava por impuesto marginal en el período en que se paga.
    /// Si es null, se asume que todo el bruto es fijo (comportamiento anterior).
    /// </param>
    /// <param name="remainingPeriodsInYear">
    /// Períodos que el empleado realmente trabajará en lo que resta del año fiscal.
    /// Se usa para quien ingresa a mitad de año: sin esto la proyección lo trataría como si
    /// hubiera cobrado ese salario desde enero y lo empujaría a un tramo más alto.
    /// Si es null, se proyecta el año completo.
    /// </param>
    /// <param name="earnedSoFarThisYear">
    /// Lo realmente devengado en el año antes de este período. Solo se usa junto con
    /// <paramref name="remainingPeriodsInYear"/>.
    /// </param>
    /// <returns>Resultado del cálculo ISR</returns>
    public async Task<IncomeTaxResult> CalculateIncomeTaxAsync(
        int companyId,
        decimal grossPay,
        string payFrequency,
        int dependents,
        bool isSubjectToIncomeTax,
        bool isSubjectToEducationalInsurance,
        DateTime calculationDate,
        decimal? variablePay = null,
        decimal? remainingPeriodsInYear = null,
        decimal earnedSoFarThisYear = 0m)
    {
        // Si no está sujeto a ISR, retorna ceros
        if (!isSubjectToIncomeTax)
        {
            return new IncomeTaxResult(
                TaxableIncome: 0,
                DependentDeduction: 0,
                SeDeduction: 0,
                NetTaxableIncome: 0,
                TaxAmount: 0,
                EffectiveTaxRate: 0
            );
        }

        var year = calculationDate.Year;

        // 1. Proyectar el ingreso anual.
        //    Solo se proyecta el salario FIJO: los ingresos variables (horas extra, comisiones)
        //    no se repiten necesariamente, y proyectarlos sobre-retiene a quien tuvo un mes
        //    excepcional. Se gravan aparte, por impuesto marginal, en el paso 5b.
        var variable = variablePay ?? 0m;
        var fixedPay = grossPay - variable;

        var annualIncome = remainingPeriodsInYear.HasValue
            // Ingreso a mitad de año: lo que falta por devengar más lo ya devengado.
            ? ProjectPartialYearIncome(fixedPay, payFrequency, remainingPeriodsInYear.Value, earnedSoFarThisYear)
            : ProjectAnnualIncome(fixedPay, payFrequency);

        // 2. Obtener configuración vigente (tasas y deducciones)
        var config = await _configProvider.GetTaxConfigAsync(companyId, calculationDate);
        if (config == null)
        {
            throw new InvalidOperationException(
                $"No se encontró configuración de ISR para companyId={companyId} en fecha {calculationDate:yyyy-MM-dd}");
        }

        // 3. Deducciones aplicables a la RETENCIÓN DE PLANILLA.
        //
        //    La deducción básica de B/. 800 (Art. 709 núm. 2, modificado por el Art. 25 de la
        //    Ley 8 de 2010) NO se aplica aquí. La ley la concede a los cónyuges "cuando presenten
        //    su declaración en forma conjunta", y en planilla el empleador no puede saber si la
        //    pareja declarará así. Si se dedujera y luego no declararan en conjunta, la retención
        //    quedaría corta y la contingencia ante la DGI recaería sobre la empresa. El empleado
        //    retiene normal durante el año y reclama el saldo a favor en su declaración.
        //
        //    Tampoco son B/. 800 "por dependiente": es una deducción única por pareja. Lo que sí
        //    existe por dependiente son los gastos escolares, que van con comprobantes y tope propio.
        var dependentDeduction = 0m;

        var seDeduction = isSubjectToEducationalInsurance
            ? annualIncome * config.EducationalInsuranceEmployeeRate / 100m
            : 0m;

        // 4. Ingreso neto gravable (después de las deducciones del Art. 709).
        //    Se mantiene el valor exacto (sin redondeo intermedio) para aplicar la tarifa.
        var netTaxableIncome = Math.Max(0, annualIncome - dependentDeduction - seDeduction);

        // 5. Aplicar brackets progresivos de ISR (Art. 700)
        var annualTax = await ApplyTaxBracketsAsync(companyId, netTaxableIncome, year);

        // 5b. Impuesto marginal de los ingresos variables (Art. 700 sobre la base combinada).
        //     Se calcula cuánto sube el impuesto anual al añadir el variable y esa diferencia
        //     se retiene ÍNTEGRA en este período, sin arrastrarse a los siguientes.
        var marginalTax = 0m;
        if (variable > 0)
        {
            var annualWithVariable = annualIncome + variable;
            var netWithVariable = Math.Max(0, annualWithVariable - dependentDeduction
                - (isSubjectToEducationalInsurance ? annualWithVariable * config.EducationalInsuranceEmployeeRate / 100m : 0m));
            var taxWithVariable = await ApplyTaxBracketsAsync(companyId, netWithVariable, year);
            marginalTax = Math.Max(0, taxWithVariable - annualTax);
        }

        // 6. Convertir impuesto anual a retención del período.
        //    Se reparte entre periodos EQUIVALENTES (P x 13/12), no entre los P del año:
        //    el décimo también tributa y su parte la retiene el módulo de décimo, que divide
        //    entre 13 (= 2/26 del anual en quincenal). Dividir aquí entre P haría que la
        //    planilla regular retuviera el 100% del impuesto y el décimo cobrara encima.
        var equivalentPeriods = PayrollConstants.GetEquivalentPeriodsPerYear(payFrequency);
        var periodTax = RoundingPolicy.Round(annualTax / equivalentPeriods + marginalTax, 2);

        // 7. Calcular tasa efectiva de impuesto
        var effectiveTaxRate = annualIncome > 0
            ? RoundingPolicy.Round((annualTax / annualIncome) * 100, 2)
            : 0;

        return new IncomeTaxResult(
            TaxableIncome: annualIncome,
            DependentDeduction: dependentDeduction,
            SeDeduction: RoundingPolicy.Round(seDeduction, 2),
            NetTaxableIncome: RoundingPolicy.Round(netTaxableIncome, 2),
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
    /// Proyecta el ingreso del año para quien no trabaja los doce meses.
    ///
    ///   renta proyectada = salario fijo x periodos restantes + lo ya devengado
    ///
    /// Se le suma la parte proporcional de décimo que le corresponde a esos períodos, para que
    /// la base siga siendo comparable con la del año completo.
    /// </summary>
    private decimal ProjectPartialYearIncome(
        decimal periodIncome, string payFrequency, decimal remainingPeriods, decimal earnedSoFar)
    {
        var porDevengar = periodIncome * remainingPeriods;
        var total = porDevengar + earnedSoFar;
        // Parte de décimo correspondiente: el décimo es 1/12 del salario anual.
        var periodsPerYear = PayrollConstants.GetPeriodsPerYear(payFrequency);
        var decimoProporcional = periodsPerYear > 0 ? total / 12m : 0m;
        return total + decimoProporcional;
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
