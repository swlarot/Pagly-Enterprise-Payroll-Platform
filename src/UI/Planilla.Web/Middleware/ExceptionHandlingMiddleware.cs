using System.Net;
using System.Text.Json;

namespace Vorluno.Planilla.Web.Middleware;

/// <summary>
/// Middleware global que captura excepciones no manejadas y retorna JSON consistente.
/// Nunca expone stack traces en producción.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción no manejada en {Method} {Path}", context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // No sobrescribir si la respuesta ya comenzó
        if (context.Response.HasStarted) return;

        var (statusCode, message) = exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "No autorizado."),
            KeyNotFoundException        => (HttpStatusCode.NotFound,     "Recurso no encontrado."),
            ArgumentException           => (HttpStatusCode.BadRequest,   exception.Message),
            InvalidOperationException   => (HttpStatusCode.BadRequest,   exception.Message),
            _                          => (HttpStatusCode.InternalServerError, "Ocurrió un error interno. Por favor intente de nuevo.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = (int)statusCode;

        var response = _env.IsDevelopment()
            ? new { message, detail = exception.Message, trace = exception.StackTrace }
            : (object)new { message };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.Response.WriteAsync(json);
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
