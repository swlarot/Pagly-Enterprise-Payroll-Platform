// ====================================================================
// Planilla - CssCalculationServicePortable
// Source: Core360 Stage 4, Sección 3
// Portado: 2025-12-26
// Descripción: Servicio de cálculo CSS (Caja de Seguro Social) según Reforma CSS de Panamá
// Cambios vs Core360:
//   - Eliminado ILogger (opcional)
//   - Reemplazado repositorio por IPayrollConfigProvider
//   - Agregado RoundingPolicy para todos los cálculos
//   - Lanza InvalidOperationException si falta configuración (NO fallback silencioso)
// ====================================================================

using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Helpers;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Application.Results;

namespace Vorluno.Planilla.Application.Services;

/// <summary>
/// Servicio de cálculo de CSS (Caja de Seguro Social) según Reforma CSS de Panamá.
/// Implementa topes variables de cotización según años cotizados y salario promedio.
/// </summary>
public class CssCalculationServicePortable
{
    private readonly IPayrollConfigProvider _configProvider;

    public CssCalculationServicePortable(IPayrollConfigProvider configProvider)
    {
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
    }

    /// <summary>
    /// Determina el tope de cotización CSS aplicable al empleado según Ley 462.
    /// </summary>
    /// <param name="companyId">ID de compañía</param>
    /// <param name="yearsCotized">Años cotizados por el empleado</param>
    /// <param name="averageSalaryLast10Years">Salario promedio últimos 10 años</param>
    /// <param name="calculationDate">Fecha de cálculo</param>
    /// <returns>Tupla con el tope aplicable y tipo de tope</returns>
    private async Task<(decimal Cap, string TipoTope)> DetermineCssCapAsync(
        int companyId,
        int yearsCotized,
        decimal averageSalaryLast10Years,
        DateTime calculationDate)
    {
        var config = await _configProvider.GetTaxConfigAsync(companyId, calculationDate);
        if (config == null)
        {
            throw new InvalidOperationException(
                $"No se encontró configuración de CSS activa para companyId={companyId} en fecha {calculationDate:yyyy-MM-dd}. " +
                "Verifique que exista una configuración vigente en PayrollTaxConfigurations.");
        }

        // Determinar tope según Ley 462, Art. 178, numeral 2
        // Tope Alto: 30+ años cotizados Y promedio >= B/. 2,500
        if (yearsCotized >= config.CssHighMinYears &&
            averageSalaryLast10Years >= config.CssHighMinAvgSalary)
        {
            return (config.CssMaxContributionBaseHigh, "Alto"); // B/. 2,500
        }

        // Tope Intermedio: 25+ años cotizados Y promedio >= B/. 2,000
        if (yearsCotized >= config.CssIntermediateMinYears &&
            averageSalaryLast10Years >= config.CssIntermediateMinAvgSalary)
        {
            return (config.CssMaxContributionBaseIntermediate, "Intermedio"); // B/. 2,000
        }

        // Tope Estándar: por defecto
        return (config.CssMaxContributionBaseStandard, "Estándar"); // B/. 1,500
    }

    /// <summary>
    /// Calcula el aporte CSS del empleado (deducción de nómina).
    /// </summary>
    /// <param name="companyId">ID de compañía</param>
    /// <param name="grossPay">Salario bruto del período</param>
    /// <param name="yearsCotized">Años cotizados por el empleado</param>
    /// <param name="averageSalaryLast10Years">Salario promedio últimos 10 años</param>
    /// <param name="isSubjectToCss">Indica si el empleado está sujeto a CSS</param>
    /// <param name="calculationDate">Fecha de cálculo (para determinar configuración vigente)</param>
    /// <returns>Resultado del cálculo CSS empleado</returns>
    public async Task<CssCalculationResult> CalculateEmployeeCssAsync(
        int companyId,
        decimal grossPay,
        string payFrequency,
        int yearsCotized,
        decimal averageSalaryLast10Years,
        bool isSubjectToCss,
        DateTime calculationDate)
    {
        if (!isSubjectToCss)
        {
            return new CssCalculationResult(
                ContributionBase: 0,
                MaxContributionBase: 0,
                TipoTope: "N/A",
                Rate: 0,
                Amount: 0
            );
        }

        var (cap, tipoTope) = await DetermineCssCapAsync(
            companyId, yearsCotized, averageSalaryLast10Years, calculationDate);

        var config = await _configProvider.GetTaxConfigAsync(companyId, calculationDate);
        if (config == null)
        {
            throw new InvalidOperationException(
                $"No se encontró configuración de CSS activa para companyId={companyId} en fecha {calculationDate:yyyy-MM-dd}");
        }

        // Prorratear tope mensual según frecuencia de pago
        var periodsPerYear = PayrollConstants.GetPeriodsPerYear(payFrequency);
        var periodCap = RoundingPolicy.Round(cap * 12m / periodsPerYear, 2);

        var contributionBase = grossPay; // No existe tope de cotización CSS (Art. 178 Ley 462 — tope aplica solo a pensión)
        var rate = config.CssEmployeeRate;
        var amount = RoundingPolicy.CalculatePercentage(contributionBase, rate);

        return new CssCalculationResult(
            ContributionBase: contributionBase,
            MaxContributionBase: periodCap,
            TipoTope: tipoTope,
            Rate: rate,
            Amount: amount
        );
    }

    /// <summary>
    /// Calcula el aporte CSS del empleador (costo patronal).
    /// Usa tasa escalonada según período (13.25% / 14.25% / 15.25%).
    /// </summary>
    /// <param name="companyId">ID de compañía</param>
    /// <param name="grossPay">Salario bruto del período</param>
    /// <param name="yearsCotized">Años cotizados por el empleado</param>
    /// <param name="averageSalaryLast10Years">Salario promedio últimos 10 años</param>
    /// <param name="isSubjectToCss">Indica si el empleado está sujeto a CSS</param>
    /// <param name="calculationDate">Fecha de cálculo (para determinar configuración vigente)</param>
    /// <returns>Resultado del cálculo CSS empleador</returns>
    public async Task<CssCalculationResult> CalculateEmployerCssAsync(
        int companyId,
        decimal grossPay,
        string payFrequency,
        int yearsCotized,
        decimal averageSalaryLast10Years,
        bool isSubjectToCss,
        DateTime calculationDate)
    {
        if (!isSubjectToCss)
        {
            return new CssCalculationResult(
                ContributionBase: 0,
                MaxContributionBase: 0,
                TipoTope: "N/A",
                Rate: 0,
                Amount: 0
            );
        }

        var (cap, tipoTope) = await DetermineCssCapAsync(
            companyId, yearsCotized, averageSalaryLast10Years, calculationDate);

        var config = await _configProvider.GetTaxConfigAsync(companyId, calculationDate);
        if (config == null)
        {
            throw new InvalidOperationException(
                $"No se encontró configuración de CSS activa para companyId={companyId} en fecha {calculationDate:yyyy-MM-dd}");
        }

        // Prorratear tope mensual según frecuencia de pago
        var periodsPerYear = PayrollConstants.GetPeriodsPerYear(payFrequency);
        var periodCap = RoundingPolicy.Round(cap * 12m / periodsPerYear, 2);

        var contributionBase = grossPay; // No existe tope de cotización CSS (Art. 178 Ley 462 — tope aplica solo a pensión)
        var rate = config.CssEmployerBaseRate;
        var amount = RoundingPolicy.CalculatePercentage(contributionBase, rate);

        return new CssCalculationResult(
            ContributionBase: contributionBase,
            MaxContributionBase: periodCap,
            TipoTope: tipoTope,
            Rate: rate,
            Amount: amount
        );
    }

    /// <summary>
    /// Calcula el aporte de riesgo profesional (costo patronal).
    /// La tasa depende del nivel de riesgo del empleado/puesto.
    /// </summary>
    /// <param name="companyId">ID de compañía</param>
    /// <param name="grossPay">Salario bruto del período</param>
    /// <param name="yearsCotized">Años cotizados por el empleado</param>
    /// <param name="averageSalaryLast10Years">Salario promedio últimos 10 años</param>
    /// <param name="cssRiskPercentage">Porcentaje de riesgo del empleado/puesto</param>
    /// <param name="isSubjectToCss">Indica si el empleado está sujeto a CSS</param>
    /// <param name="calculationDate">Fecha de cálculo</param>
    /// <returns>Tupla con monto de riesgo y tasa aplicada</returns>
    public async Task<(decimal Amount, decimal Rate)> CalculateRiskContributionAsync(
        int companyId,
        decimal grossPay,
        string payFrequency,
        int yearsCotized,
        decimal averageSalaryLast10Years,
        decimal cssRiskPercentage,
        bool isSubjectToCss,
        DateTime calculationDate)
    {
        if (!isSubjectToCss)
        {
            return (0, 0);
        }

        var (cap, _) = await DetermineCssCapAsync(
            companyId, yearsCotized, averageSalaryLast10Years, calculationDate);

        var config = await _configProvider.GetTaxConfigAsync(companyId, calculationDate);
        if (config == null)
        {
            throw new InvalidOperationException(
                $"No se encontró configuración de CSS activa para companyId={companyId} en fecha {calculationDate:yyyy-MM-dd}");
        }

        // Prorratear tope mensual según frecuencia de pago
        var periodsPerYear = PayrollConstants.GetPeriodsPerYear(payFrequency);
        var periodCap = RoundingPolicy.Round(cap * 12m / periodsPerYear, 2);

        // No existe tope de cotización CSS — solo aplica a pensión (Art. 178 Ley 462)
        var contributionBase = grossPay;

        // Usar la tasa del empleado directamente (Acuerdo N°2 de 1995: 0.56/0.98/2.10/3.64/5.67%)
        // El campo CssRiskPercentage almacena el valor como porcentaje (ej: 2.10 = 2.10%)
        decimal riskRate = cssRiskPercentage;

        var amount = RoundingPolicy.CalculatePercentage(contributionBase, riskRate);

        return (amount, riskRate);
    }

    /// <summary>
    /// Calcula CSS completo (empleado + empleador + riesgo profesional).
    /// Método orquestador que llama a los cálculos individuales.
    /// </summary>
    /// <param name="companyId">ID de compañía</param>
    /// <param name="grossPay">Salario bruto del período</param>
    /// <param name="yearsCotized">Años cotizados por el empleado</param>
    /// <param name="averageSalaryLast10Years">Salario promedio últimos 10 años</param>
    /// <param name="cssRiskPercentage">Porcentaje de riesgo del empleado/puesto</param>
    /// <param name="isSubjectToCss">Indica si el empleado está sujeto a CSS</param>
    /// <param name="calculationDate">Fecha de cálculo</param>
    /// <returns>Resultado completo de cálculo CSS</returns>
    public async Task<CssFullCalculationResult> CalculateFullCssAsync(
        int companyId,
        decimal grossPay,
        string payFrequency,
        int yearsCotized,
        decimal averageSalaryLast10Years,
        decimal cssRiskPercentage,
        bool isSubjectToCss,
        DateTime calculationDate)
    {
        var employeeCss = await CalculateEmployeeCssAsync(
            companyId, grossPay, payFrequency, yearsCotized, averageSalaryLast10Years, isSubjectToCss, calculationDate);

        var employerCss = await CalculateEmployerCssAsync(
            companyId, grossPay, payFrequency, yearsCotized, averageSalaryLast10Years, isSubjectToCss, calculationDate);

        var (riskAmount, riskRate) = await CalculateRiskContributionAsync(
            companyId, grossPay, payFrequency, yearsCotized, averageSalaryLast10Years, cssRiskPercentage, isSubjectToCss, calculationDate);

        return new CssFullCalculationResult(
            EmployeeCss: employeeCss,
            EmployerCss: employerCss,
            RiskContribution: riskAmount,
            RiskRate: riskRate,
            TotalEmployerCost: employerCss.Amount + riskAmount
        );
    }
}
