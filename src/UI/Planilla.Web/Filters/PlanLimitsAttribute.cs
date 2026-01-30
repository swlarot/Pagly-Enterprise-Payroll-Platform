using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Vorluno.Planilla.Application.Interfaces;

namespace Vorluno.Planilla.Web.Filters;

/// <summary>
/// Action filter que valida límites del plan de suscripción antes de ejecutar una acción.
/// Este filtro es CRÍTICO para enforcement de límites de negocio en el modelo SaaS.
/// </summary>
/// <example>
/// Uso:
/// [PlanLimits(PlanLimitType.CreateEmployee)]
/// public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeDto dto)
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class PlanLimitsAttribute : Attribute, IAsyncActionFilter
{
    private readonly PlanLimitType _limitType;

    public PlanLimitsAttribute(PlanLimitType limitType)
    {
        _limitType = limitType;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 1. Obtener TenantId del usuario autenticado
        var tenantIdClaim = context.HttpContext.User.FindFirst("tenant_id");
        if (tenantIdClaim == null || !int.TryParse(tenantIdClaim.Value, out var tenantId) || tenantId <= 0)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                error = "UNAUTHORIZED",
                message = "No se pudo identificar tu empresa. Por favor inicia sesión nuevamente."
            });
            return;
        }

        // 2. Obtener el servicio de límites de plan
        var planLimitService = context.HttpContext.RequestServices.GetService<IPlanLimitService>();
        if (planLimitService == null)
        {
            context.Result = new ObjectResult(new
            {
                error = "CONFIGURATION_ERROR",
                message = "Error de configuración del servidor. Por favor contacta soporte."
            })
            {
                StatusCode = 500
            };
            return;
        }

        // 3. Verificar límite según el tipo
        (bool allowed, string? reason) = _limitType switch
        {
            PlanLimitType.CreateEmployee => await planLimitService.CanCreateEmployeeAsync(tenantId),
            PlanLimitType.InviteUser => await planLimitService.CanInviteUserAsync(tenantId),
            PlanLimitType.ExportExcel => await planLimitService.CanExportExcelAsync(tenantId),
            PlanLimitType.ExportPdf => await planLimitService.CanExportPdfAsync(tenantId),
            _ => (false, "Tipo de límite no reconocido")
        };

        // 4. Si no está permitido, retornar error con mensaje específico
        if (!allowed)
        {
            context.Result = new BadRequestObjectResult(new
            {
                error = "PLAN_LIMIT_REACHED",
                message = reason ?? "Has alcanzado el límite de tu plan actual",
                limitType = _limitType.ToString(),
                upgradeUrl = "/subscription/upgrade"
            });
            return;
        }

        // 5. Límite OK, continuar con la ejecución del endpoint
        await next();
    }
}

/// <summary>
/// Tipos de límites de plan que se pueden verificar
/// </summary>
public enum PlanLimitType
{
    /// <summary>
    /// Verificar si se pueden crear empleados
    /// </summary>
    CreateEmployee,

    /// <summary>
    /// Verificar si se pueden invitar usuarios
    /// </summary>
    InviteUser,

    /// <summary>
    /// Verificar si se pueden exportar archivos Excel
    /// </summary>
    ExportExcel,

    /// <summary>
    /// Verificar si se pueden exportar archivos PDF
    /// </summary>
    ExportPdf
}
