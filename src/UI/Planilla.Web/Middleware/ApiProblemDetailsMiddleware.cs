using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vorluno.Planilla.Web.Middleware;

/// <summary>
/// Middleware de manejo de errores para el API Platform B2B (rutas <c>/v1/*</c>).
/// Retorna respuestas en formato RFC 7807 (Problem Details) con extensiones tipo Stripe:
/// <list type="bullet">
///   <item><c>type</c>: URI identificadora del tipo de error (relative).</item>
///   <item><c>title</c>: resumen legible del problema.</item>
///   <item><c>status</c>: código HTTP.</item>
///   <item><c>detail</c>: explicación específica (en dev incluye exception.Message).</item>
///   <item><c>instance</c>: path de la request que falló.</item>
///   <item><c>errorCode</c>: código estable tipo Stripe (<c>invalid_request_error</c>, etc.).</item>
///   <item><c>requestId</c>: correlation ID para soporte.</item>
/// </list>
///
/// <para>
/// IMPORTANTE: Solo actúa en rutas que empiecen con <c>/v1/</c>. Para el resto (SaaS
/// actual con formato legacy), llama a <c>_next(context)</c> sin capturar nada, y el
/// <c>ExceptionHandlingMiddleware</c> legacy sigue funcionando como antes.
/// </para>
/// </summary>
public class ApiProblemDetailsMiddleware
{
    private const string ApiPathPrefix = "/v1";
    private const string ProblemJsonContentType = "application/problem+json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiProblemDetailsMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ApiProblemDetailsMiddleware(
        RequestDelegate next,
        ILogger<ApiProblemDetailsMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Solo aplicamos ProblemDetails a rutas del API Platform (v1/*).
        // Para el resto, dejamos pasar el request sin interferir — el ExceptionHandlingMiddleware
        // legacy maneja las excepciones con el shape antiguo.
        if (!context.Request.Path.StartsWithSegments(ApiPathPrefix))
        {
            await _next(context);
            return;
        }

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex,
                    "Excepción después de que la respuesta comenzó en {Method} {Path} — no se puede enviar ProblemDetails",
                    context.Request.Method, context.Request.Path);
                throw;
            }

            var problem = BuildProblemFromException(context, ex);
            _logger.LogError(ex,
                "API Platform error {ErrorCode} ({Status}) en {Method} {Path} — RequestId={RequestId}",
                problem.ErrorCode, problem.Status, context.Request.Method, context.Request.Path, problem.RequestId);

            await WriteProblemAsync(context, problem);
        }
    }

    private ApiProblemDetails BuildProblemFromException(HttpContext context, Exception exception)
    {
        var (status, errorCode, title) = exception switch
        {
            ArgumentException => (400, "invalid_request_error", "Invalid request"),
            InvalidOperationException => (400, "invalid_request_error", "Invalid request"),
            UnauthorizedAccessException => (401, "authentication_error", "Authentication failed"),
            KeyNotFoundException => (404, "not_found_error", "Resource not found"),
            NotImplementedException => (501, "api_error", "Not implemented"),
            _ => (500, "api_error", "Internal server error")
        };

        return new ApiProblemDetails
        {
            Type = $"https://pagly.com/errors/{errorCode}",
            Title = title,
            Status = status,
            Detail = _env.IsDevelopment() || status == 400
                ? exception.Message
                : "An unexpected error occurred. Please contact support with the requestId for assistance.",
            Instance = context.Request.Path.Value ?? string.Empty,
            ErrorCode = errorCode,
            RequestId = context.GetRequestId()
        };
    }

    private static async Task WriteProblemAsync(HttpContext context, ApiProblemDetails problem)
    {
        context.Response.Clear();
        context.Response.StatusCode = problem.Status;
        context.Response.ContentType = ProblemJsonContentType;

        var json = JsonSerializer.Serialize(problem, JsonOptions);
        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// DTO del shape de error del API Platform (RFC 7807 + extensiones Stripe-style).
/// </summary>
public class ApiProblemDetails
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string Instance { get; set; } = string.Empty;

    /// <summary>
    /// Código estable del tipo de error, tipo Stripe (ej: "invalid_request_error").
    /// A diferencia de <c>type</c> (URI), este campo es para parseo programático.
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Correlation ID del request (X-Request-Id). Usar para soporte.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;
}

public static class ApiProblemDetailsMiddlewareExtensions
{
    public static IApplicationBuilder UseApiProblemDetailsMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<ApiProblemDetailsMiddleware>();
}
