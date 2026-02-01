using System.ComponentModel.DataAnnotations;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa un permiso específico asignado a un rol personalizado.
/// Los permisos son granulares y controlan acceso a funcionalidades específicas.
/// </summary>
public class RolePermission : BaseEntity, ITenantEntity
{
    /// <summary>
    /// ID del tenant (heredado del rol)
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// ID del rol al que pertenece este permiso
    /// </summary>
    public int CustomTenantRoleId { get; set; }

    /// <summary>
    /// Clave del permiso en formato "recurso.acción"
    /// Ejemplos: "employees.read", "payroll.calculate", "reports.export"
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Permission { get; set; } = string.Empty;

    /// <summary>
    /// Fecha en que se otorgó el permiso
    /// </summary>
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    // Navegación
    /// <summary>
    /// Rol al que pertenece este permiso
    /// </summary>
    public virtual CustomTenantRole Role { get; set; } = null!;

    /// <summary>
    /// Tenant al que pertenece este permiso (navegación indirecta)
    /// </summary>
    public virtual Tenant Tenant { get; set; } = null!;
}
