using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Web.Authentication;

namespace Vorluno.Planilla.Web.Filters;

/// <summary>
/// Marca un endpoint como idempotente. Requiere que el cliente envíe el header
/// <c>Idempotency-Key: &lt;uuid&gt;</c>; si no lo envía, el endpoint se ejecuta
/// normalmente (idempotencia es opt-in del cliente).
///
/// <para>
/// Flujo:
/// <list type="number">
///   <item>Si no hay header <c>Idempotency-Key</c> → pasa al siguiente filter.</item>
///   <item>Si el auth no produjo un <c>api_key_id</c> claim → pasa (endpoints
///         JWT-only no aplican idempotencia por ahora).</item>
///   <item>Se calcula el hash SHA256 del body para detectar abuso del mismo key.</item>
///   <item>Se busca en IdempotencyStore por (ApiKeyId + IdempotencyKey).</item>
///   <item>Match con mismo hash → <b>replay</b>: retorna la response cacheada tal cual.</item>
///   <item>Match con hash distinto → 422 "Idempotency-Key reusado con payload distinto".</item>
///   <item>Sin match → ejecuta el endpoint, captura la response y la guarda en DB.</item>
/// </list>
/// </para>
///
/// <para>
/// La ventana de retención es fija en 24h (constante de la clase). Si en el futuro
/// algún endpoint necesita una ventana distinta, se puede parametrizar en el ctor.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class IdempotentAttribute : Attribute, IAsyncActionFilter
{
    /// <summary>
    /// Header HTTP estándar de la industria (Stripe, Twilio).
    /// </summary>
    public const string HeaderName = "Idempotency-Key";

    /// <summary>
    /// Máximo tamaño permitido del valor del header (Stripe exige UUID = 36 chars).
    /// Aceptamos más porque los clientes pueden usar su propio naming.
    /// </summary>
    private const int MaxKeyLength = 255;

    /// <summary>
    /// Retención de los records. Más allá de 24h se considera stale; un retry
    /// al día siguiente no debería contar como el mismo request.
    /// </summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;

        // 1. ¿Tiene el header? Si no, pasamos — idempotencia es opt-in.
        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues)
            || keyValues.Count == 0)
        {
            await next();
            return;
        }

        var idempotencyKey = keyValues[0];
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            context.Result = ProblemResult(httpContext, 400,
                "INVALID_IDEMPOTENCY_KEY",
                "El header Idempotency-Key no puede estar vacío.");
            return;
        }

        if (idempotencyKey.Length > MaxKeyLength)
        {
            context.Result = ProblemResult(httpContext, 400,
                "INVALID_IDEMPOTENCY_KEY",
                $"El header Idempotency-Key excede el máximo de {MaxKeyLength} caracteres.");
            return;
        }

        // 2. Sólo aplicamos idempotencia a requests autenticados con API key
        // (no a JWT del dashboard — esos endpoints no cobran por uso).
        var apiKeyIdClaim = httpContext.User.FindFirst(ApiKeyAuthenticationHandler.ClaimApiKeyId);
        if (apiKeyIdClaim == null || !int.TryParse(apiKeyIdClaim.Value, out var apiKeyId))
        {
            await next();
            return;
        }

        // 3. Leemos el body (EnableBuffering() se llamó en Program.cs; si no,
        // esta lectura consumiría el stream. Ver configuración de middleware).
        var requestBody = await ReadRequestBodyAsync(httpContext);
        var requestHash = ComputeSha256(requestBody);

        // 4. Lookup en el store
        var store = httpContext.RequestServices.GetRequiredService<IIdempotencyStore>();
        var cancellationToken = httpContext.RequestAborted;

        var existing = await store.TryGetAsync(apiKeyId, idempotencyKey!, cancellationToken);

        if (existing != null)
        {
            // 4.a. Match exacto del hash → replay
            if (existing.RequestHash == requestHash
                && existing.Endpoint == httpContext.Request.Path.Value)
            {
                var replayResult = new ContentResult
                {
                    StatusCode = existing.StatusCode,
                    Content = existing.ResponseJson,
                    ContentType = "application/json",
                };
                httpContext.Response.Headers.Append("Idempotent-Replay", "true");
                httpContext.Response.Headers.Append("Idempotent-Created", existing.CreatedAt.ToString("O"));
                context.Result = replayResult;
                return;
            }

            // 4.b. Mismo key pero payload/endpoint distinto = abuso del cliente
            context.Result = ProblemResult(httpContext, 422,
                "IDEMPOTENCY_KEY_REUSED",
                "El Idempotency-Key fue usado previamente con un payload distinto. " +
                "Genera un UUID nuevo para este request o envía el payload original.");
            return;
        }

        // 5. Sin match → ejecuta el endpoint, captura y guarda
        var executedContext = await next();
        if (executedContext.Exception != null) return; // no cachear excepciones no manejadas

        var (statusCode, responseJson) = await ExtractResponseAsync(executedContext);
        if (statusCode == 0) return; // no hay response que cachear

        await store.SaveAsync(
            apiKeyId: apiKeyId,
            idempotencyKey: idempotencyKey!,
            endpoint: httpContext.Request.Path.Value ?? "",
            requestHash: requestHash,
            statusCode: statusCode,
            responseJson: responseJson,
            ttl: Ttl,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Lee el body del request desde el inicio. Requiere EnableBuffering().
    /// </summary>
    private static async Task<string> ReadRequestBodyAsync(HttpContext ctx)
    {
        if (!ctx.Request.Body.CanSeek) ctx.Request.EnableBuffering();

        ctx.Request.Body.Position = 0;
        using var reader = new StreamReader(
            ctx.Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        ctx.Request.Body.Position = 0;
        return body;
    }

    /// <summary>
    /// Extrae status + json de la response. Maneja los 3 ActionResult más comunes
    /// que genera ASP.NET Core: ObjectResult, JsonResult, ContentResult.
    /// </summary>
    private static async Task<(int, string)> ExtractResponseAsync(
        ActionExecutedContext executedContext)
    {
        if (executedContext.Result is ObjectResult objectResult)
        {
            var status = objectResult.StatusCode ?? 200;
            var json = objectResult.Value == null
                ? "null"
                : JsonSerializer.Serialize(objectResult.Value, JsonOptions);
            return (status, json);
        }

        if (executedContext.Result is JsonResult jsonResult)
        {
            var status = jsonResult.StatusCode ?? 200;
            var json = jsonResult.Value == null
                ? "null"
                : JsonSerializer.Serialize(jsonResult.Value, JsonOptions);
            return (status, json);
        }

        if (executedContext.Result is ContentResult contentResult
            && contentResult.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (contentResult.StatusCode ?? 200, contentResult.Content ?? "");
        }

        // Responses no-JSON (p.ej. FileResult) no se cachean
        await Task.CompletedTask;
        return (0, "");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string ComputeSha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Retorna un ObjectResult con el shape RFC 7807 que ya usan los endpoints /v1/*.
    /// </summary>
    private static IActionResult ProblemResult(HttpContext ctx, int status, string code, string detail)
    {
        return new ObjectResult(new
        {
            type = $"https://pagly.dev/problems/{code.ToLowerInvariant()}",
            title = code,
            status,
            detail,
            instance = ctx.Request.Path.Value,
            requestId = ctx.Items["RequestId"] as string,
        })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }
}
