using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa una ausencia laboral de un empleado
/// </summary>
public class Ausencia
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    /// <summary>
    /// ID del tenant al que pertenece esta ausencia
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Tipo de ausencia
    /// </summary>
    public TipoAusencia TipoAusencia { get; set; }

    /// <summary>
    /// Fecha de inicio de la ausencia
    /// </summary>
    public DateTime FechaInicio { get; set; }

    /// <summary>
    /// Fecha de fin de la ausencia
    /// </summary>
    public DateTime FechaFin { get; set; }

    /// <summary>
    /// Días de ausencia (puede ser 0.5 para medio día)
    /// </summary>
    public decimal DiasAusencia { get; set; }

    /// <summary>
    /// Motivo de la ausencia
    /// </summary>
    public string Motivo { get; set; } = string.Empty;

    /// <summary>
    /// Indica si tiene justificación (certificado médico, etc.)
    /// </summary>
    public bool TieneJustificacion { get; set; }

    /// <summary>
    /// Número de certificado médico, permiso, etc.
    /// </summary>
    public string? DocumentoReferencia { get; set; }

    /// <summary>
    /// Indica si la ausencia afecta el salario (default true para injustificadas)
    /// </summary>
    public bool AfectaSalario { get; set; }

    /// <summary>
    /// Monto descontado por la ausencia
    /// </summary>
    public decimal? MontoDescontado { get; set; }

    /// <summary>
    /// Usuario que aprobó/registró la ausencia
    /// </summary>
    public string? AprobadoPor { get; set; }

    /// <summary>
    /// ID del detalle de planilla cuando se descuenta
    /// </summary>
    public int? PlanillaDetailId { get; set; }

    public string? Observaciones { get; set; }

    // Auditoría
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public virtual Tenant? Tenant { get; set; }
}
