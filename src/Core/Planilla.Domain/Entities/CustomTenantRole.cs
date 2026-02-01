using System.ComponentModel.DataAnnotations;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa un rol personalizado creado por el Owner del tenant.
/// Permite definir roles con nombres y permisos granulares específicos para cada empresa.
/// </summary>
public class CustomTenantRole : BaseEntity, ITenantEntity
{
    /// <summary>
    /// ID del tenant al que pertenece este rol
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Nombre personalizado del rol (ej: "Gerente de RRHH", "Contador Senior", "Supervisor de Planilla")
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descripción del rol y sus responsabilidades
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Indica si es un rol del sistema (Owner, Admin, Manager, Accountant, Employee)
    /// Los roles del sistema NO pueden ser editados ni eliminados.
    /// </summary>
    public bool IsSystem { get; set; } = false;

    /// <summary>
    /// Color hexadecimal para representar el rol en la UI (ej: "#3B82F6")
    /// </summary>
    [StringLength(7)]
    public string? Color { get; set; }

    /// <summary>
    /// Orden de visualización (menor número = mayor prioridad)
    /// </summary>
    public int DisplayOrder { get; set; } = 0;

    // Navegación
    /// <summary>
    /// Tenant al que pertenece este rol
    /// </summary>
    public virtual Tenant Tenant { get; set; } = null!;

    /// <summary>
    /// Permisos asignados a este rol
    /// </summary>
    public virtual ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();

    /// <summary>
    /// Usuarios que tienen este rol asignado
    /// </summary>
    public virtual ICollection<TenantUser> Users { get; set; } = new List<TenantUser>();
}
