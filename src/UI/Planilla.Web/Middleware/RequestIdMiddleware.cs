namespace Vorluno.Planilla.Web.Middleware;

/// <summary>
/// Asigna un identificador único a cada request HTTP y lo expone en:
///   - <c>HttpContext.Items["RequestId"]</c> (para acceso desde middlewares/controllers)
///   - Header de respuesta <c>X-Request-Id</c> (para que el cliente lo conserve)
///
/// Si el cliente envía el header <c>X-Request-Id</c> en la request, se respeta.
/// Esto permite que un cliente del API Platform propague un correlation ID entre
/// servicios (pattern estándar en plataformas tipo Stripe/Twilio).
///
/// Límite de 64 caracteres para evitar abuso con headers gigantes.
/// </summary>
public class RequestIdMiddleware
{
    public const string HeaderName = "X-Request-Id";
    public const string ContextKey = "RequestId";
    private const int MaxLength = 64;

    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = ResolveRequestId(context);

        context.Items[ContextKey] = requestId;

        // Set directo del header: este middleware corre ANTES que cualquier otro que
        // pueda escribir a la response, así que siempre es seguro. OnStarting también
        // existiría como opción pero requiere que el servidor (Kestrel) lo dispare
        // — en tests con MemoryStream no se dispara, causando falsos negativos.
        context.Response.Headers[HeaderName] = requestId;

        await _next(context);
    }

    private static string ResolveRequestId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var clientProvided))
        {
            var candidate = clientProvided.ToString();
            if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length <= MaxLength)
            {
                return candidate;
            }
        }

        // Fallback: GUID sin guiones para compatibilidad con clientes que no aceptan caracteres especiales
        return Guid.NewGuid().ToString("N");
    }
}

public static class RequestIdMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestIdMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<RequestIdMiddleware>();

    /// <summary>
    /// Helper para obtener el RequestId desde HttpContext. Retorna string.Empty si no está seteado
    /// (por ejemplo, en tests o rutas antes del middleware).
    /// </summary>
    public static string GetRequestId(this HttpContext context)
    {
        return context.Items.TryGetValue(RequestIdMiddleware.ContextKey, out var value) && value is string s
            ? s
            : string.Empty;
    }
}
