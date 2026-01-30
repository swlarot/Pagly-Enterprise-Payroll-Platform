using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa un refresh token JWT para mantener sesiones activas sin requerir login frecuente.
/// Los refresh tokens tienen vida larga (7-30 días) y se usan para obtener nuevos access tokens.
/// SEGURIDAD: Tokens revocados no pueden usarse, implementa rotación de tokens.
/// </summary>
public class RefreshToken
{
    /// <summary>
    /// ID único del refresh token (GUID para mayor seguridad)
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// ID del usuario al que pertenece este token
    /// </summary>
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// ID del tenant al que pertenece el usuario (para multi-tenancy)
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Token aleatorio único (256 bits de entropía)
    /// </summary>
    [Required]
    [StringLength(512)]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Fecha de expiración del refresh token (típicamente 7-30 días)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Fecha de creación del token
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indica si el token ha sido revocado (logout, cambio de contraseña, etc.)
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    /// <summary>
    /// Fecha en que se revocó el token (si aplica)
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Razón de revocación (logout, password_changed, suspicious_activity, etc.)
    /// </summary>
    [StringLength(100)]
    public string? RevokedReason { get; set; }

    /// <summary>
    /// IP address desde donde se creó el token (para auditoría)
    /// </summary>
    [StringLength(45)]
    public string? CreatedByIp { get; set; }

    /// <summary>
    /// Token que reemplazó a este (para rotación de tokens)
    /// </summary>
    public Guid? ReplacedByTokenId { get; set; }

    // Navigation property
    public virtual AppUser? User { get; set; }
    public virtual Tenant? Tenant { get; set; }

    /// <summary>
    /// Verifica si el token es válido (no expirado y no revocado)
    /// </summary>
    public bool IsActive => !IsRevoked && DateTime.UtcNow < ExpiresAt;
}
