using System.ComponentModel.DataAnnotations;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa la relación entre un usuario y un tenant,
/// incluyendo el rol del usuario dentro del tenant.
/// </summary>
public class TenantUser : BaseEntity, ITenantEntity
{
    /// <summary>
    /// ID del tenant
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// ID del usuario de ASP.NET Identity
    /// </summary>
    [Required]
    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Rol del usuario dentro del tenant (Owner o User)
    /// </summary>
    public TenantRole Role { get; set; } = TenantRole.User;

    /// <summary>
    /// ID del rol personalizado asignado al usuario (null si usa rol del sistema)
    /// </summary>
    public int? CustomTenantRoleId { get; set; }

    /// <summary>
    /// Fecha en que el usuario se unió al tenant
    /// </summary>
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha del último inicio de sesión del usuario en este tenant
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Invitación pendiente (si el usuario aún no ha aceptado)
    /// </summary>
    public bool IsPendingInvitation { get; set; } = false;

    /// <summary>
    /// Token de invitación
    /// </summary>
    [StringLength(200)]
    public string? InvitationToken { get; set; }

    /// <summary>
    /// Fecha de expiración de la invitación
    /// </summary>
    public DateTime? InvitationExpiresAt { get; set; }

    /// <summary>
    /// Email al que se envió la invitación
    /// </summary>
    [StringLength(200)]
    public string? InvitedEmail { get; set; }

    // Navegación
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual AppUser? User { get; set; }
    public virtual CustomTenantRole? CustomRole { get; set; }

    /// <summary>
    /// Verifica si el usuario es Owner del tenant
    /// </summary>
    public bool IsOwner()
    {
        return Role == TenantRole.Owner;
    }

    /// <summary>
    /// Verifica si el usuario puede gestionar empleados
    /// Los permisos específicos para User se determinan mediante CustomTenantRole
    /// </summary>
    public bool CanManageEmployees()
    {
        // Solo verificación básica - permisos granulares se verifican en controllers
        return Role == TenantRole.Owner || CustomTenantRoleId.HasValue;
    }

    /// <summary>
    /// Verifica si el usuario puede ver reportes
    /// Los permisos específicos para User se determinan mediante CustomTenantRole
    /// </summary>
    public bool CanViewReports()
    {
        // Solo verificación básica - permisos granulares se verifican en controllers
        return Role == TenantRole.Owner || CustomTenantRoleId.HasValue;
    }
}
