using System.ComponentModel.DataAnnotations;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa un tenant (inquilino) en el sistema multi-tenant.
/// Cada tenant es una empresa que usa Vorluno Planilla.
/// </summary>
public class Tenant : BaseEntity
{
    /// <summary>
    /// Nombre de la empresa/tenant
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Subdominio único para el tenant (ej: empresa.planilla.vorluno.dev)
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Subdomain { get; set; } = string.Empty;

    /// <summary>
    /// Registro Único de Contribuyente (RUC) de Panamá
    /// </summary>
    [StringLength(20)]
    public string? RUC { get; set; }

    /// <summary>
    /// Dígito Verificador del RUC
    /// </summary>
    [StringLength(10)]
    public string? DV { get; set; }

    /// <summary>
    /// Dirección física de la empresa
    /// </summary>
    [StringLength(500)]
    public string? Address { get; set; }

    /// <summary>
    /// Teléfono de contacto
    /// </summary>
    [StringLength(20)]
    public string? Phone { get; set; }

    /// <summary>
    /// Email de contacto del tenant
    /// </summary>
    [StringLength(200)]
    public string? Email { get; set; }

    /// <summary>
    /// Configuración del tenant en formato JSON
    /// </summary>
    public string? Settings { get; set; }

    // Navegación
    public int? SubscriptionId { get; set; }
    public virtual Subscription? Subscription { get; set; }

    public virtual ICollection<TenantUser> Users { get; set; } = new List<TenantUser>();
    public virtual ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
    public virtual ICollection<PayrollHeader> PayrollHeaders { get; set; } = new List<PayrollHeader>();
}
