using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Estados de un anticipo de salario
/// </summary>
public enum EstadoAnticipo
{
    Pendiente = 1,
    Aprobado = 2,
    Descontado = 3,
    Rechazado = 4,
    Cancelado = 5
}

/// <summary>
/// Representa un anticipo de salario solicitado por un empleado.
/// </summary>
public class Anticipo
{
    public int Id { get; set; }

    /// <summary>
    /// ID del empleado que solicita el anticipo
    /// </summary>
    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    /// <summary>
    /// ID del tenant al que pertenece este anticipo
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Monto del anticipo solicitado
    /// </summary>
    public decimal Monto { get; set; }

    /// <summary>
    /// Fecha en que se solicitó el anticipo
    /// </summary>
    public DateTime FechaSolicitud { get; set; }

    /// <summary>
    /// Fecha en que fue aprobado el anticipo
    /// </summary>
    public DateTime? FechaAprobacion { get; set; }

    /// <summary>
    /// Fecha de la planilla en la que se descontará el anticipo
    /// </summary>
    public DateTime FechaDescuento { get; set; }

    /// <summary>
    /// Estado actual del anticipo
    /// </summary>
    public EstadoAnticipo Estado { get; set; }

    /// <summary>
    /// Motivo de la solicitud del anticipo
    /// </summary>
    public string Motivo { get; set; } = string.Empty;

    /// <summary>
    /// Usuario que aprobó el anticipo
    /// </summary>
    public string? AprobadoPor { get; set; }

    /// <summary>
    /// Observaciones adicionales
    /// </summary>
    public string? Observaciones { get; set; }

    /// <summary>
    /// ID de la planilla donde se descontó (cuando estado = Descontado)
    /// </summary>
    public int? PlanillaId { get; set; }

    // Auditoría
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public virtual Tenant? Tenant { get; set; }
}
