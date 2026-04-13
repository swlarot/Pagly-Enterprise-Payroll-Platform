using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Registro de una respuesta cacheada por idempotency key.
/// Evita doble ejecución (y doble facturación) cuando el cliente reintenta un POST
/// por timeout de red — el segundo request con el mismo <c>Idempotency-Key</c>
/// retorna la respuesta original sin re-ejecutar el cálculo.
///
/// <para>
/// Patrón estándar de la industria (Stripe, Twilio, Avalara):
///   1. Cliente envía POST con header <c>Idempotency-Key: &lt;uuid&gt;</c>
///   2. Server busca match por (<c>ApiKeyId</c> + <c>IdempotencyKey</c>)
///   3. Si existe + request hash coincide → replay response cacheada
///   4. Si existe + hash diferente → 422 (abuso del key)
///   5. Si no existe → procesa normalmente, guarda response antes de devolver
/// </para>
///
/// <para>
/// Retención: 24 horas (configurable). Se purga con background job o al leer expirados.
/// La ventana de 24h cubre timeouts + retries automáticos de librerías cliente típicas.
/// Más allá de eso, un re-intento con el mismo key es considerado stale y se re-ejecuta.
/// </para>
///
/// <para>
/// No implementa <see cref="Interfaces.ITenantEntity"/> a propósito:
///   - El gate de aislamiento es (<c>ApiKeyId</c> + <c>IdempotencyKey</c>),
///     ya unique por diseño (dos tenants distintos pueden reusar el mismo UUID).
///   - El filter lee por <c>ApiKeyId</c> del auth, no necesita resolver tenant primero.
/// </para>
/// </summary>
public class IdempotencyRecord
{
    public int Id { get; set; }

    /// <summary>
    /// API key que hizo el request original. FK a ApiKeys.Id.
    /// Componente del match junto con IdempotencyKey — dos tenants con el
    /// mismo UUID en el header NO colisionan.
    /// </summary>
    public int ApiKeyId { get; set; }

    /// <summary>
    /// UUID o string arbitrario (hasta 255 chars) que el cliente provee en
    /// el header <c>Idempotency-Key</c>. Stripe exige UUID v4; nosotros aceptamos
    /// cualquier string no vacío, acotado a 255.
    /// </summary>
    [Required]
    [StringLength(255)]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hex del body del request original. Se compara con el hash del
    /// request actual — si difiere, es abuso del key (mismo key, payload distinto)
    /// y se devuelve 422 Unprocessable Entity.
    /// </summary>
    [Required]
    [StringLength(64)]
    public string RequestHash { get; set; } = string.Empty;

    /// <summary>
    /// Path del endpoint donde se usó el key (ej: "/v1/payroll/calculate").
    /// Protege contra el caso raro de reusar el key en endpoint distinto.
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Status code de la response cacheada (200, 400, 422, etc).
    /// Se replay-ea tal cual, incluyendo errores — si el cálculo falló con 400,
    /// el retry devuelve 400 sin re-ejecutar (idempotencia incluye errores).
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// JSON de la response. Puede ser grande (breakdown completo de planilla).
    /// Columna text en Postgres, sin cap explícito — el rate limiter limita volumen.
    /// </summary>
    [Required]
    public string ResponseJson { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp UTC del request original.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp UTC de expiración. Default: CreatedAt + 24h.
    /// Requests con timestamp &gt; ExpiresAt se tratan como no existentes.
    /// </summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);

    // Navigation
    public virtual ApiKey? ApiKey { get; set; }
}
