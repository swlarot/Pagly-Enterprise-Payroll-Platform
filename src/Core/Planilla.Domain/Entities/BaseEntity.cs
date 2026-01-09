using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Clase base para todas las entidades del sistema multi-tenant.
/// Proporciona propiedades comunes de auditoría y soft delete.
/// </summary>
public abstract class BaseEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Fecha de creación del registro
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha de última actualización del registro
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Indica si el registro está activo (soft delete)
    /// </summary>
    public bool IsActive { get; set; } = true;
}
