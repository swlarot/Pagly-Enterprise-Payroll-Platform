using System.ComponentModel.DataAnnotations;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa una invitación para que un usuario se una a un tenant.
/// Las invitaciones tienen un token único y fecha de expiración.
/// </summary>
public class TenantInvitation : BaseEntity
{
    /// <summary>
    /// ID del tenant que envía la invitación
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Email del usuario invitado
    /// </summary>
    [Required]
    [StringLength(200)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Rol que se le asignará al usuario cuando acepte la invitación
    /// </summary>
    public TenantRole Role { get; set; } = TenantRole.Employee;

    /// <summary>
    /// Token único de la invitación (GUID)
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Token { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Fecha de expiración de la invitación (7 días desde creación)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// ID del usuario que creó la invitación
    /// </summary>
    [Required]
    [StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// Fecha en que se aceptó la invitación (null si aún no se ha aceptado)
    /// </summary>
    public DateTime? AcceptedAt { get; set; }

    /// <summary>
    /// Indica si la invitación fue revocada
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    // Navegación
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual AppUser CreatedBy { get; set; } = null!;

    /// <summary>
    /// Verifica si la invitación está vigente (no expirada, no aceptada, no revocada)
    /// </summary>
    public bool IsValid()
    {
        return !IsRevoked
            && AcceptedAt == null
            && ExpiresAt > DateTime.UtcNow
            && IsActive;
    }

    /// <summary>
    /// Verifica si la invitación ya fue aceptada
    /// </summary>
    public bool IsAccepted()
    {
        return AcceptedAt.HasValue;
    }

    /// <summary>
    /// Verifica si la invitación expiró
    /// </summary>
    public bool IsExpired()
    {
        return ExpiresAt <= DateTime.UtcNow;
    }
}
