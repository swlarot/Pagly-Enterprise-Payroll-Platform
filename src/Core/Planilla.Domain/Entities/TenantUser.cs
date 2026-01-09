using System.ComponentModel.DataAnnotations;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa la relación entre un usuario y un tenant,
/// incluyendo el rol del usuario dentro del tenant.
/// </summary>
public class TenantUser : BaseEntity
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
    /// Rol del usuario dentro del tenant
    /// </summary>
    public TenantRole Role { get; set; } = TenantRole.Employee;

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

    /// <summary>
    /// Verifica si el usuario tiene un rol de administrador o superior
    /// </summary>
    public bool IsAdminOrOwner()
    {
        return Role == TenantRole.Owner || Role == TenantRole.Admin;
    }

    /// <summary>
    /// Verifica si el usuario puede gestionar empleados
    /// </summary>
    public bool CanManageEmployees()
    {
        return Role == TenantRole.Owner ||
               Role == TenantRole.Admin ||
               Role == TenantRole.Manager;
    }

    /// <summary>
    /// Verifica si el usuario puede ver reportes
    /// </summary>
    public bool CanViewReports()
    {
        return Role != TenantRole.Employee;
    }
}
