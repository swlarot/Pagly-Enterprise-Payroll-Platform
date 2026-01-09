using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Registra eventos de auditoría para cumplimiento y seguridad.
/// Cada entrada captura quién hizo qué, cuándo y desde dónde.
/// </summary>
public class AuditLogEntry : BaseEntity
{
    /// <summary>
    /// ID del tenant al que pertenece esta entrada de auditoría
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// ID del usuario que realizó la acción
    /// </summary>
    [Required]
    [StringLength(450)]
    public string ActorUserId { get; set; } = string.Empty;

    /// <summary>
    /// Email del usuario que realizó la acción (desnormalizado para histórico)
    /// </summary>
    [Required]
    [StringLength(200)]
    public string ActorEmail { get; set; } = string.Empty;

    /// <summary>
    /// Acción realizada (ej: "EmployeeCreated", "InviteCreated", "TenantUpdated")
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de entidad afectada (ej: "Employee", "TenantInvitation", "Tenant")
    /// </summary>
    [Required]
    [StringLength(100)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// ID de la entidad afectada (como string para mayor flexibilidad)
    /// </summary>
    [StringLength(100)]
    public string? EntityId { get; set; }

    /// <summary>
    /// Dirección IP desde donde se realizó la acción
    /// </summary>
    [StringLength(50)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User-Agent del navegador/cliente
    /// </summary>
    [StringLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Metadatos adicionales en formato JSON (sin PII sensible)
    /// </summary>
    public string? MetadataJson { get; set; }

    // Navegación
    public virtual Tenant Tenant { get; set; } = null!;
}
