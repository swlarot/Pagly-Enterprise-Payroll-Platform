using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
[EnableRateLimiting("calculator-v1")]
public class CalculatorController : ControllerBase
{
    private readonly PayrollCalculationOrchestratorPortable _orchestrator;

    public CalculatorController(
        [FromKeyedServices("static")] PayrollCalculationOrchestratorPortable orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Calcula las deducciones legales (CSS + Seguro Educativo + ISR) y costos patronales
    /// para un empleado en un período. Es una operación <b>stateless</b> — el caller
    /// provee todos los datos del empleado en el request; el servicio no consulta ninguna
    /// base de datos ni guarda estado.
    ///
    /// <para>
    /// Reglas aplicadas:
    /// <list type="bullet">
    ///   <item>CSS según Ley 462 (3 fases escalonadas: 13.25% hasta feb 2027, 14.25% hasta feb 2029, 15.25% desde mar 2029).</item>
    ///   <item>Seguro Educativo 1.25% empleado / 1.50% empleador (sin tope).</item>
    ///   <item>ISR progresivo con brackets DGI y proyección anual ×13 meses (incluye décimo).</item>
    ///   <item>Riesgo profesional según Acuerdo N°2 de 1995.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="request">Parámetros del empleado y período. Todos los campos son requeridos excepto <c>calculationDate</c> (default UtcNow).</param>
    /// <param name="cancellationToken">Token de cancelación del request HTTP. ASP.NET Core lo inyecta automáticamente.</param>
    /// <returns>Breakdown completo con neto, deducciones del empleado, costos patronales y metadata de versión.</returns>
    /// <response code="200">Cálculo exitoso. Retorna el breakdown completo.</response>
    /// <response code="400">Parámetros inválidos (ej: <c>grossPay ≤ 0</c>, frecuencia de pago desconocida). El body sigue RFC 7807 con <c>errorCode: "invalid_request_error"</c>.</response>
    /// <response code="401">Header <c>X-Api-Key</c> faltante, malformado, revocado o expirado. El mensaje de error es opaco — no revela si la key no existe, fue revocada o el secret es incorrecto.</response>
    /// <response code="429">Rate limit excedido para esta API key. Retorna <c>Retry-After</c> header indicando segundos hasta que se pueda reintentar. Por default el límite es 60 requests/minuto por key.</response>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(PayrollCalculateResponse), 200)]
    [ProducesResponseType(typeof(Vorluno.Planilla.Web.Middleware.ApiProblemDetails), 400)]
    [ProducesResponseType(typeof(Vorluno.Planilla.Web.Middleware.ApiProblemDetails), 401)]
    [ProducesResponseType(typeof(Vorluno.Planilla.Web.Middleware.ApiProblemDetails), 429)]
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
