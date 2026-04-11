using System.ComponentModel.DataAnnotations;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// API key emitida a un tenant del API Platform B2B.
///
/// Formato de la key: <c>pk_{mode}_{prefix}{secret}</c>, donde:
///   - <c>mode</c>: "test" o "live"
///   - <c>prefix</c>: 8 caracteres hex aleatorios (almacenados en claro, indexados para lookup O(1))
///   - <c>secret</c>: 32 caracteres hex aleatorios (NO almacenados — solo el hash SHA256)
///
/// La key completa solo es visible al usuario UNA vez, al momento de creación.
/// Después solo guardamos <c>KeyPrefix</c> (indexado, unique) y <c>KeyHash</c>.
///
/// Para validar una key:
///   1. Parsear "pk_live_abc12345xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
///      → mode=Live, prefix="abc12345", secret="xxxx..."
///   2. Lookup por KeyPrefix (índice único) — O(1)
///   3. Verificar hash del secret con comparación constant-time
///   4. Verificar IsActive, RevokedAt, ExpiresAt, Mode
/// </summary>
public class ApiKey : BaseEntity, ITenantEntity
{
    /// <summary>
    /// Tenant owner de esta key. FK a Tenants.Id.
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Nombre amigable asignado por el usuario (ej: "Urbis Production", "Dev Laptop").
    /// No tiene valor de seguridad — es para que el tenant identifique sus keys en el dashboard.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Prefijo público de 8 caracteres hex (lookup). Indexado UNIQUE a nivel global.
    /// Nunca es un secreto — aparece en logs, dashboards, y responses.
    /// Ejemplo: "abc12345" (los chars después de "pk_live_" en la key completa).
    /// </summary>
    [Required]
    [StringLength(16)]
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hex del secret (32 chars después del prefix en la key completa).
    /// 64 caracteres hex. Nunca se retorna al cliente.
    /// Usado solo para validación con comparación constant-time.
    /// </summary>
    [Required]
    [StringLength(128)]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    /// "Test" o "Live". Las test keys pueden llamar endpoints sandbox (futuros),
    /// las live keys llaman producción real. Separación tipo Stripe.
    /// </summary>
    [Required]
    [StringLength(10)]
    public string Mode { get; set; } = "Live";

    /// <summary>
    /// Fecha de expiración opcional. Si es null, la key no expira.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Última vez que la key fue usada exitosamente (para UI del dashboard).
    /// Update es fire-and-forget / batch (no bloquea requests).
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Timestamp de revocación manual (por rotate o delete).
    /// Una key con RevokedAt != null ya no es válida aunque IsActive=true.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Motivo de revocación (ej: "rotate", "compromised", "user_delete").
    /// </summary>
    [StringLength(100)]
    public string? RevocationReason { get; set; }

    /// <summary>
    /// Contador monotónico de requests totales hechos con esta key.
    /// Se incrementa en el middleware de usage tracking (chunk futuro).
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// UserId (AspNetUsers.Id es string GUID) del creador.
    /// Auditoría — quién creó esta key desde el dashboard.
    /// </summary>
    [StringLength(450)]
    public string? CreatedByUserId { get; set; }

    // Navigation
    public virtual Tenant? Tenant { get; set; }

    /// <summary>
    /// True si la key es actualmente utilizable: activa, no revocada, no expirada.
    /// Helper para el handler de autenticación.
    /// </summary>
    public bool IsUsableNow(DateTime? now = null)
    {
        var nowUtc = now ?? DateTime.UtcNow;
        if (!IsActive) return false;
        if (RevokedAt.HasValue) return false;
        if (ExpiresAt.HasValue && ExpiresAt.Value <= nowUtc) return false;
        return true;
    }
}
