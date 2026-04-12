using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Web.Authentication;

namespace Vorluno.Planilla.Web.Middleware;

/// <summary>
/// Middleware de tracking de uso del API Platform B2B.
///
/// <para>
/// Scope: solo rutas que empiezan con <c>/v1/</c>. Otras rutas pasan sin tocar nada.
/// </para>
///
/// <para>
/// Dos responsabilidades:
/// <list type="bullet">
///   <item><b>ApiUsageRecord</b>: inserta una fila por CADA request (200, 400, 401, 429, 500)
///         con endpoint, method, status code, latencia. Es la base de datos de analytics.</item>
///   <item><b>TotalRequests counter</b>: UPDATE atómico en ApiKey solo para 2xx (cálculos
///         ejecutados exitosamente). Es el contador rápido del dashboard.</item>
/// </list>
/// </para>
///
/// <para>
/// Ambas operaciones son best-effort (log+swallow en caso de error de DB). El response
/// al cliente ya se envió — no bloqueamos por fallos de tracking.
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
        // Scope guard: solo rutas del API Platform.
        if (!context.Request.Path.StartsWithSegments(ApiPathPrefix))
        {
            await _next(context);
            return;
        }

        // Cronometrar el request completo (controller + middlewares downstream).
        var stopwatch = Stopwatch.StartNew();

        await _next(context);

        stopwatch.Stop();

        // Extraer datos del request para el record.
        var statusCode = context.Response.StatusCode;
        var endpoint = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;
        var responseTimeMs = (int)stopwatch.ElapsedMilliseconds;

        // Intentar obtener el api_key_id del claim (puede ser null si auth falló).
        int? apiKeyId = null;
        int tenantId = 0;
        var keyIdClaim = context.User.FindFirst(ApiKeyAuthenticationHandler.ClaimApiKeyId);
        var tenantIdClaim = context.User.FindFirst(ApiKeyAuthenticationHandler.ClaimTenantId);

        if (keyIdClaim is not null && int.TryParse(keyIdClaim.Value, out var parsedKeyId) && parsedKeyId > 0)
        {
            apiKeyId = parsedKeyId;
        }

        if (tenantIdClaim is not null && int.TryParse(tenantIdClaim.Value, out var parsedTenantId))
        {
            tenantId = parsedTenantId;
        }

        // ── 1. Insertar ApiUsageRecord (TODOS los status codes) ──
        try
        {
            var record = new ApiUsageRecord
            {
                TenantId = tenantId,
                ApiKeyId = apiKeyId,
                Endpoint = endpoint.Length > 200 ? endpoint[..200] : endpoint,
                Method = method,
                StatusCode = statusCode,
                ResponseTimeMs = responseTimeMs,
                CreatedAt = DateTime.UtcNow
            };

            db.ApiUsageRecords.Add(record);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ApiUsageTracking: falló inserción de ApiUsageRecord para {Method} {Endpoint} (status {StatusCode}). " +
                "El request ya se retornó al cliente — este record de analytics se perdió.",
                method, endpoint, statusCode);
        }

        // ── 2. Incrementar TotalRequests solo para 2xx con api_key_id ──
        if (statusCode >= 200 && statusCode < 300 && apiKeyId.HasValue)
        {
            try
            {
                var now = DateTime.UtcNow;
                await db.ApiKeys
                    .IgnoreQueryFilters()
                    .Where(k => k.Id == apiKeyId.Value)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(k => k.TotalRequests, k => k.TotalRequests + 1)
                        .SetProperty(k => k.LastUsedAt, now));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ApiUsageTracking: falló incremento de TotalRequests para ApiKey {ApiKeyId}.",
                    apiKeyId);
            }
        }
    }
}

public static class ApiUsageTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UseApiUsageTrackingMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<ApiUsageTrackingMiddleware>();
}
