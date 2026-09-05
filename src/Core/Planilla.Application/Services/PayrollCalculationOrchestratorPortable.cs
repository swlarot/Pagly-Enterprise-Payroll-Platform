// ====================================================================
// Planilla - PayrollCalculationOrchestratorPortable
// Source: Core360 Stage 4, Sección 6
// Portado: 2025-12-26
// Descripción: Orquestador de cálculo de planilla para un empleado
// Combina CSS + Seguro Educativo + ISR en un solo resultado
// ====================================================================

using Vorluno.Planilla.Application.Results;

namespace Vorluno.Planilla.Application.Services;

/// <summary>
/// Orquestador que calcula la planilla completa de un empleado.
/// Coordina los servicios de CSS, Seguro Educativo e ISR para producir
/// el resultado final con todas las deducciones y costos patronales.
/// </summary>
public class PayrollCalculationOrchestratorPortable
{
    private readonly CssCalculationServicePortable _cssService;
    private readonly EducationalInsuranceServicePortable _educationalInsuranceService;
    private readonly IncomeTaxCalculationServicePortable _incomeTaxService;

    public PayrollCalculationOrchestratorPortable(
        CssCalculationServicePortable cssService,
        EducationalInsuranceServicePortable educationalInsuranceService,
        IncomeTaxCalculationServicePortable incomeTaxService)
    {
        _cssService = cssService ?? throw new ArgumentNullException(nameof(cssService));
        _educationalInsuranceService = educationalInsuranceService ?? throw new ArgumentNullException(nameof(educationalInsuranceService));
        _incomeTaxService = incomeTaxService ?? throw new ArgumentNullException(nameof(incomeTaxService));
    }

    /// <summary>
    /// Calcula la planilla completa de un empleado, incluyendo todas las deducciones y costos patronales.
    /// </summary>
    /// <param name="companyId">ID de compañía</param>
    /// <param name="grossPay">Salario bruto del período</param>
    /// <param name="payFrequency">Frecuencia de pago (Quincenal, Mensual, Semanal)</param>
    /// <param name="yearsCotized">Años cotizados por el empleado (para determinar tope CSS)</param>
    /// <param name="averageSalaryLast10Years">Salario promedio últimos 10 años (para tope CSS)</param>
    /// <param name="cssRiskPercentage">Porcentaje de riesgo profesional (0.56, 2.50, o 5.39)</param>
    /// <param name="dependents">Número de dependientes declarados (para deducción ISR)</param>
    /// <param name="isSubjectToCss">Indica si el empleado está sujeto a CSS</param>
    /// <param name="isSubjectToEducationalInsurance">Indica si el empleado está sujeto a Seguro Educativo</param>
    /// <param name="isSubjectToIncomeTax">Indica si el empleado está sujeto a ISR</param>
    /// <param name="calculationDate">Fecha de cálculo (para determinar configuración vigente)</param>
    /// <param name="variablePay">
    /// Parte variable del bruto (horas extra, recargos, comisiones). No se proyecta al año:
    /// se grava por impuesto marginal en el período en que se paga.
    /// </param>
    /// <param name="remainingPeriodsInYear">
    /// Períodos que el empleado trabajará en lo que resta del año fiscal. Para quien ingresa
    /// a mitad de año, evita anualizar su salario como si lo hubiera cobrado desde enero.
    /// </param>
    /// <param name="earnedSoFarThisYear">Lo realmente devengado en el año antes de este período.</param>
    /// <returns>Resultado completo del cálculo de planilla</returns>
    public async Task<PayrollCalculationResult> CalculateEmployeePayrollAsync(
        int companyId,
        decimal grossPay,
        string payFrequency,
        int yearsCotized,
        decimal averageSalaryLast10Years,
        decimal cssRiskPercentage,
        int dependents,
        bool isSubjectToCss,
        bool isSubjectToEducationalInsurance,
        bool isSubjectToIncomeTax,
        DateTime calculationDate,
        decimal? variablePay = null,
        decimal? remainingPeriodsInYear = null,
        decimal earnedSoFarThisYear = 0m)
    {
        // ====================================================================
        // 1. Calcular CSS completo (empleado + empleador + riesgo profesional)
        // ====================================================================
        var cssResult = await _cssService.CalculateFullCssAsync(
            companyId,
            grossPay,
            payFrequency,
            yearsCotized,
            averageSalaryLast10Years,
            cssRiskPercentage,
            isSubjectToCss,
            calculationDate
        );

        decimal cssEmployee = cssResult.EmployeeCss.Amount;
        decimal cssEmployer = cssResult.EmployerCss.Amount;
        decimal riskContribution = cssResult.RiskContribution;

        // ====================================================================
        // 2. Calcular Seguro Educativo completo (empleado + empleador)
        // IMPORTANTE: SE NO tiene tope máximo, se aplica sobre salario completo
        // ====================================================================
        var seResult = await _educationalInsuranceService.CalculateFullInsuranceAsync(
            companyId,
            grossPay,
            isSubjectToEducationalInsurance,
            calculationDate
        );

        decimal educationalInsuranceEmployee = seResult.EmployeeDeduction;
        decimal educationalInsuranceEmployer = seResult.EmployerContribution;

        // ====================================================================
        // 3. Calcular Impuesto Sobre la Renta (ISR)
        // Proyección anual con brackets progresivos
        // ====================================================================
        var isrResult = await _incomeTaxService.CalculateIncomeTaxAsync(
            companyId,
            grossPay,
            payFrequency,
            dependents,
            isSubjectToIncomeTax,
            isSubjectToEducationalInsurance,
            calculationDate,
            variablePay,
            remainingPeriodsInYear,
            earnedSoFarThisYear
        );

        decimal incomeTax = isrResult.TaxAmount;

        // ====================================================================
        // 4. Calcular totales del empleado
        // ====================================================================
        decimal totalDeductions = cssEmployee + educationalInsuranceEmployee + incomeTax;
        decimal netPay = grossPay - totalDeductions;

        // ====================================================================
        // 5. Calcular totales del empleador (costos patronales)
        // ====================================================================
        decimal totalEmployerCost = cssEmployer + riskContribution + educationalInsuranceEmployer;

        // ====================================================================
        // 6. Construir resultado final
        // ====================================================================
        return new PayrollCalculationResult(
            GrossPay: grossPay,
            CssEmployee: cssEmployee,
            EducationalInsuranceEmployee: educationalInsuranceEmployee,
            IncomeTax: incomeTax,
            TotalDeductions: totalDeductions,
            NetPay: netPay,
            CssEmployer: cssEmployer,
            EducationalInsuranceEmployer: educationalInsuranceEmployer,
            RiskContribution: riskContribution,
            TotalEmployerCost: totalEmployerCost
        );
    }

    /// <summary>
    /// Calcula la planilla para múltiples empleados en lote.
    /// NOTA: Método auxiliar para procesar planillas completas.
    /// </summary>
    /// <param name="companyId">ID de compañía</param>
    /// <param name="employeeCalculations">Lista de parámetros de cálculo por empleado</param>
    /// <param name="calculationDate">Fecha de cálculo</param>
    /// <returns>Lista de resultados de cálculo, uno por empleado</returns>
    public async Task<List<PayrollCalculationResult>> CalculateBatchPayrollAsync(
        int companyId,
        List<EmployeeCalculationParameters> employeeCalculations,
        DateTime calculationDate)
    {
        var results = new List<PayrollCalculationResult>();

        foreach (var empCalc in employeeCalculations)
        {
            var result = await CalculateEmployeePayrollAsync(
                companyId,
                empCalc.GrossPay,
                empCalc.PayFrequency,
                empCalc.YearsCotized,
                empCalc.AverageSalaryLast10Years,
                empCalc.CssRiskPercentage,
                empCalc.Dependents,
                empCalc.IsSubjectToCss,
                empCalc.IsSubjectToEducationalInsurance,
                empCalc.IsSubjectToIncomeTax,
                calculationDate
            );

            results.Add(result);
        }

        return results;
    }
}

/// <summary>
/// Parámetros de cálculo para un empleado.
/// Clase auxiliar para procesamiento en lote.
/// </summary>
public record EmployeeCalculationParameters(
    int EmployeeId,
    decimal GrossPay,
    string PayFrequency,
    int YearsCotized,
    decimal AverageSalaryLast10Years,
    decimal CssRiskPercentage,
    int Dependents,
    bool IsSubjectToCss,
    bool IsSubjectToEducationalInsurance,
    bool IsSubjectToIncomeTax
);
