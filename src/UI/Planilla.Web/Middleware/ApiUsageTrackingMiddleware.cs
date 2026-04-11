using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Web.Authentication;

namespace Vorluno.Planilla.Web.Middleware;

/// <summary>
/// Middleware de tracking de uso del API Platform B2B.
///
/// <para>
/// Scope: solo rutas que empiezan con <c>/v1/</c>. Otras rutas (SaaS actual,
/// healthchecks, SPA) pasan sin tocar nada.
/// </para>
///
/// <para>
/// Comportamiento: después de que el controller retorna, si:
/// <list type="bullet">
///   <item>El path empezó con <c>/v1/</c></item>
///   <item>La request fue autenticada con scheme <c>"ApiKey"</c> (claim <c>api_key_id</c> presente)</item>
///   <item>El status code es 2xx (llamada exitosa, el cálculo se ejecutó)</item>
/// </list>
/// …entonces ejecuta un UPDATE atómico en la tabla <c>ApiKeys</c>:
/// <c>TotalRequests = TotalRequests + 1, LastUsedAt = UtcNow</c>.
/// </para>
///
/// <para>
/// Por qué atómico con <c>ExecuteUpdateAsync</c> en vez de SELECT+SaveChanges:
/// <list type="bullet">
///   <item>Evita race conditions bajo concurrencia (dos requests simultáneos).</item>
///   <item>Sin tracking overhead de EF change tracker.</item>
///   <item>Single round-trip a la DB.</item>
/// </list>
/// </para>
///
/// <para>
/// Por qué solo 2xx (no 4xx): un 400 es una llamada mal formada del cliente,
/// no un cálculo ejecutado. No lo cobramos ni lo contamos. Un 401 falla antes
/// de este middleware (la auth nunca setea el claim <c>api_key_id</c>).
/// Protección contra abuso de 400s se debe hacer con rate limiting,
/// no contando requests.
/// </para>
///
/// <para>
/// Errores del UPDATE son log+swallow: el tracking es best-effort, no bloquea
/// la respuesta. Si la DB está caída para escrituras, el cálculo ya se ejecutó
/// y retornamos al cliente — perder un contador es tolerable, bloquear no.
/// </para>
/// </summary>
public class ApiUsageTrackingMiddleware
{
    private const string ApiPathPrefix = "/v1";

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiUsageTrackingMiddleware> _logger;

    public ApiUsageTrackingMiddleware(
        RequestDelegate next,
        ILogger<ApiUsageTrackingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        // Scope guard: solo trackeamos rutas del API Platform.
        if (!context.Request.Path.StartsWithSegments(ApiPathPrefix))
        {
            await _next(context);
            return;
        }

        await _next(context);

        // Después del controller — decide si contar.
        if (!ShouldTrack(context, out var apiKeyId))
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            var updated = await db.ApiKeys
                .IgnoreQueryFilters() // IX_ApiKey_KeyPrefix es global, no por tenant
                .Where(k => k.Id == apiKeyId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(k => k.TotalRequests, k => k.TotalRequests + 1)
                    .SetProperty(k => k.LastUsedAt, now));

            if (updated == 0)
            {
                _logger.LogWarning(
                    "ApiUsageTracking: no se encontró ApiKey {ApiKeyId} para incrementar contador. " +
                    "Posible revocación concurrente.",
                    apiKeyId);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: log y swallow. No bloqueamos el response.
            _logger.LogWarning(ex,
                "ApiUsageTracking: falló incremento de contador para ApiKey {ApiKeyId}. " +
                "El cálculo ya se retornó al cliente — este contador se perdió.",
                apiKeyId);
        }
    }

    /// <summary>
    /// Decide si esta request debe contarse. Condiciones:
    ///  1. Status 2xx (cálculo exitoso)
    ///  2. Auth por scheme "ApiKey" con claim <c>api_key_id</c> parseable a int
    /// </summary>
    private static bool ShouldTrack(HttpContext context, out int apiKeyId)
    {
        apiKeyId = 0;

        var status = context.Response.StatusCode;
        if (status < 200 || status >= 300)
        {
            return false;
        }

        var claim = context.User.FindFirst(ApiKeyAuthenticationHandler.ClaimApiKeyId);
        if (claim is null) return false;

        return int.TryParse(claim.Value, out apiKeyId) && apiKeyId > 0;
    }
}

public static class ApiUsageTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UseApiUsageTrackingMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<ApiUsageTrackingMiddleware>();
}
