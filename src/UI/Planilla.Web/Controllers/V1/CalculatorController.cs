using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vorluno.Planilla.Application.Configuration;
using Vorluno.Planilla.Application.DTOs.Calculator;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Web.Authentication;

namespace Vorluno.Planilla.Web.Controllers.V1;

/// <summary>
/// Endpoint público del API Platform B2B para cálculos de planilla Panamá.
///
/// <para>
/// Autenticación: header <c>X-Api-Key</c> con un key emitido desde el dashboard
/// del SaaS. El handler <see cref="ApiKeyAuthenticationHandler"/> valida y setea
/// claims <c>tenant_id</c>, <c>api_key_id</c>, <c>api_key_mode</c>.
/// </para>
///
/// <para>
/// El controller NO toca base de datos. Inyecta el orchestrator keyed
/// <c>"static"</c> que internamente usa <see cref="DefaultPanamaPayrollConfig"/>
/// con las 3 fases Ley 462 hardcoded — totalmente stateless.
/// </para>
///
/// <para>
/// Errores siguen RFC 7807 (formato <c>application/problem+json</c>) gracias al
/// <see cref="Vorluno.Planilla.Web.Middleware.ApiProblemDetailsMiddleware"/>.
/// </para>
/// </summary>
[ApiController]
[Route("v1/payroll")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
public class CalculatorController : ControllerBase
{
    private readonly PayrollCalculationOrchestratorPortable _orchestrator;

    public CalculatorController(
        [FromKeyedServices("static")] PayrollCalculationOrchestratorPortable orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Calcula las deducciones legales (CSS + SE + ISR) y costos patronales
    /// para un empleado en un período. Stateless — el caller provee todos
    /// los datos del empleado en el request.
    /// </summary>
    /// <param name="request">Parámetros del empleado y período.</param>
    /// <returns>Breakdown completo con neto, deducciones, costo patronal y metadatos.</returns>
    /// <response code="200">Cálculo exitoso.</response>
    /// <response code="400">Parámetros inválidos (ej: grossPay ≤ 0, frecuencia inválida).</response>
    /// <response code="401">X-Api-Key faltante, inválida, revocada o expirada.</response>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(PayrollCalculateResponse), 200)]
    public async Task<ActionResult<PayrollCalculateResponse>> Calculate(
        [FromBody] PayrollCalculateRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            // ModelBinder normalmente devuelve 400 automáticamente, pero por defensa:
            throw new ArgumentException("Request body is required.");
        }

        var calcDate = request.CalculationDate ?? DateTime.UtcNow;

        // El orchestrator recibe companyId=0 porque la versión "static" lo ignora
        // (StaticPayrollConfigProvider no lee BD). Pasamos 0 por contrato del método.
        var result = await _orchestrator.CalculateEmployeePayrollAsync(
            companyId: 0,
            grossPay: request.GrossPay,
            payFrequency: request.PayFrequency,
            yearsCotized: request.YearsCotized,
            averageSalaryLast10Years: request.AverageSalaryLast10Years,
            cssRiskPercentage: request.CssRiskPercentage,
            dependents: request.Dependents,
            isSubjectToCss: request.IsSubjectToCss,
            isSubjectToEducationalInsurance: request.IsSubjectToEducationalInsurance,
            isSubjectToIncomeTax: request.IsSubjectToIncomeTax,
            calculationDate: calcDate);

        var response = new PayrollCalculateResponse(
            GrossPay: result.GrossPay,
            CssEmployee: result.CssEmployee,
            EducationalInsuranceEmployee: result.EducationalInsuranceEmployee,
            IncomeTax: result.IncomeTax,
            TotalDeductions: result.TotalDeductions,
            NetPay: result.NetPay,
            CssEmployer: result.CssEmployer,
            EducationalInsuranceEmployer: result.EducationalInsuranceEmployer,
            RiskContribution: result.RiskContribution,
            TotalEmployerCost: result.TotalEmployerCost,
            Version: DefaultPanamaPayrollConfig.Version,
            CalculationDate: calcDate);

        return Ok(response);
    }
}
