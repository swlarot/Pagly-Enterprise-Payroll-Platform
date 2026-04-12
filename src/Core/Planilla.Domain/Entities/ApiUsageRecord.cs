using System.ComponentModel.DataAnnotations;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Un registro por cada request HTTP al API Platform (<c>/v1/*</c>).
/// Es la "caja negra" de uso: permite construir dashboards de analytics,
/// calcular cuotas mensuales, detectar anomalías y facturar overages.
///
/// <para>
/// A diferencia de <c>ApiKey.TotalRequests</c> (contador monotónico agregado),
/// esta tabla tiene una fila por request con timestamp, status code y latencia.
/// Eso permite queries tipo "requests por día del último mes" o "p95 latencia
/// de la key X en la última hora".
/// </para>
///
/// <para>
/// Decisiones de diseño:
/// <list type="bullet">
///   <item>Se registran TODOS los status codes (200, 400, 401, 429), no solo 2xx.
///         Un dashboard de errores necesita ver los 400s y 429s también.</item>
///   <item>NO se guarda el body del request ni del response (privacidad + storage).
///         Solo metadata: endpoint, method, status, latencia, tamaños.</item>
///   <item>Implementa <see cref="ITenantEntity"/> para aislamiento automático via
///         global query filter — el Owner del tenant solo ve sus propios records.</item>
///   <item>No hereda de BaseEntity (no necesita UpdatedAt ni IsActive — es append-only).</item>
/// </list>
/// </para>
/// </summary>
public class ApiUsageRecord
{
    public int Id { get; set; }

    /// <summary>
    /// Tenant owner de la API key que hizo el request.
    /// FK via global query filter para aislamiento automático.
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// API key que se usó para autenticar. FK a ApiKeys.Id.
    /// Null si el request falló autenticación (401 sin claim api_key_id).
    /// </summary>
    public int? ApiKeyId { get; set; }

    /// <summary>
    /// Path del endpoint llamado (ej: "/v1/payroll/calculate").
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Método HTTP (ej: "POST", "GET").
    /// </summary>
    [Required]
    [StringLength(10)]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Status code de la response (200, 400, 401, 429, 500).
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Tiempo de respuesta en milisegundos (desde que el middleware empezó
    /// a cronometrar hasta que el controller retornó). No incluye el tiempo
    /// del middleware de tracking propio (para evitar recursión circular).
    /// </summary>
    public int ResponseTimeMs { get; set; }

    /// <summary>
    /// Timestamp UTC del request. Append-only — nunca se modifica.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ApiKey? ApiKey { get; set; }
}
