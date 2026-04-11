using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Vorluno.Planilla.Application.Interfaces;

namespace Vorluno.Planilla.Web.Authentication;

/// <summary>
/// Authentication scheme para el API Platform B2B que valida el header
/// <c>X-Api-Key</c> contra la tabla ApiKeys usando <see cref="IApiKeyService"/>.
///
/// <para>
/// Nombre del scheme: <c>"ApiKey"</c>. Los controllers lo activan con
/// <c>[Authorize(AuthenticationSchemes = "ApiKey")]</c>.
/// </para>
///
/// <para>
/// Comportamiento del <see cref="HandleAuthenticateAsync"/>:
/// <list type="bullet">
///   <item>Sin header <c>X-Api-Key</c>: retorna <c>NoResult</c> (no es fail — permite que
///         otros schemes atiendan el request si están combinados).</item>
///   <item>Header presente pero inválido: retorna <c>Fail</c> con mensaje opaco.</item>
///   <item>Header válido: crea ClaimsPrincipal con:
///         <c>tenant_id</c>, <c>api_key_id</c>, <c>api_key_mode</c>, <c>auth_type=api_key</c>,
///         y <c>ClaimTypes.NameIdentifier</c>=api_key_id (para compat con middleware ASP.NET).
///   </item>
/// </list>
/// </para>
///
/// <para>
/// <b>IMPORTANTE</b>: este handler NO asigna roles (<c>tenant_role</c>).
/// Una request con API key no tiene un usuario — tiene una key que pertenece a un tenant.
/// Los endpoints del API Platform deben usar autorización basada en claims propios
/// (scope futuro) o simplemente <c>[Authorize(AuthenticationSchemes = "ApiKey")]</c> sin roles.
/// </para>
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    public const string ClaimTenantId = "tenant_id";
    public const string ClaimApiKeyId = "api_key_id";
    public const string ClaimApiKeyMode = "api_key_mode";
    public const string ClaimAuthType = "auth_type";

    private readonly IApiKeyService _apiKeyService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeyService)
        : base(options, logger, encoder)
    {
        _apiKeyService = apiKeyService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
        {
            // No hay header — no es nuestro scheme. Dejamos que JWT u otro atienda.
            return AuthenticateResult.NoResult();
        }

        var plaintextKey = values.ToString();
        if (string.IsNullOrWhiteSpace(plaintextKey))
        {
            return AuthenticateResult.Fail("Missing API key");
        }

        var apiKey = await _apiKeyService.ValidateAsync(plaintextKey, Context.RequestAborted);
        if (apiKey is null)
        {
            // Mensaje opaco — no revelar si el key no existe vs fue revocado vs hash mismatch.
            return AuthenticateResult.Fail("Invalid API key");
        }

        var claims = new List<Claim>
        {
            new(ClaimTenantId, apiKey.TenantId.ToString()),
            new(ClaimApiKeyId, apiKey.Id.ToString()),
            new(ClaimApiKeyMode, apiKey.Mode),
            new(ClaimAuthType, "api_key"),
            new(ClaimTypes.NameIdentifier, $"api_key:{apiKey.Id}")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }

    /// <summary>
    /// Cuando la autenticación falla, retornamos 401 con WWW-Authenticate
    /// en formato estándar. El cuerpo del 401 lo maneja el
    /// ApiProblemDetailsMiddleware si la ruta es <c>/v1/*</c>.
    /// </summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers["WWW-Authenticate"] = $"{SchemeName} realm=\"Pagly API\"";
        return Task.CompletedTask;
    }
}

/// <summary>
/// Opciones del scheme ApiKey. Por ahora no tiene propiedades configurables,
/// pero debe existir por contrato de AuthenticationHandler&lt;TOptions&gt;.
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}
