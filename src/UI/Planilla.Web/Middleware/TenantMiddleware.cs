using Vorluno.Planilla.Application.Interfaces;

namespace Vorluno.Planilla.Web.Middleware;

/// <summary>
/// Middleware que inicializa el contexto del tenant en cada request
/// basado en los claims del JWT token
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger, IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        try
        {
            // El TenantContext se inicializa automáticamente desde los claims del usuario
            // en su constructor, pero aquí podemos agregar validaciones adicionales

            // SKIP VALIDATION IN TESTING ENVIRONMENT (Integration Tests)
            // Testing environment uses in-memory database and relies solely on JWT claims
            if (_environment.IsEnvironment("Testing"))
            {
                await _next(context);
                return;
            }

            if (context.User?.Identity?.IsAuthenticated == true && tenantContext.HasTenant)
            {
                // El TenantContext ya obtiene el ID del claim automáticamente
                var tenantId = tenantContext.TenantId;

                if (tenantId > 0)
                {
                    // Verificar que el tenant existe y está activo
                    try
                    {
                        var tenant = await tenantContext.GetCurrentTenantAsync();

                        if (tenant == null)
                        {
                            _logger.LogWarning("Tenant {TenantId} no encontrado o inactivo", tenantId);
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            await context.Response.WriteAsJsonAsync(new
                            {
                                error = "Tenant no encontrado o inactivo"
                            });
                            return;
                        }

                        // Verificar si la suscripción está activa
                        if (tenant.Subscription != null && !tenant.Subscription.IsActiveOrTrialing())
                        {
                            _logger.LogWarning("Suscripción inactiva para tenant {TenantId}", tenantId);
                            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                            await context.Response.WriteAsJsonAsync(new
                            {
                                error = "Suscripción inactiva",
                                status = tenant.Subscription.Status.ToString()
                            });
                            return;
                        }

                        // Verificar si el trial ha expirado
                        if (tenant.Subscription != null && tenant.Subscription.IsTrialExpired())
                        {
                            _logger.LogWarning("Trial expirado para tenant {TenantId}", tenantId);
                            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                            await context.Response.WriteAsJsonAsync(new
                            {
                                error = "Período de prueba expirado",
                                message = "Por favor, actualiza tu plan de suscripción"
                            });
                            return;
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogError(ex, "Error al validar tenant {TenantId}", tenantId);
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
                        return;
                    }
                }
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en TenantMiddleware");
            throw;
        }
    }
}

/// <summary>
/// Extensión para registrar el middleware fácilmente
/// </summary>
public static class TenantMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantMiddleware>();
    }
}
