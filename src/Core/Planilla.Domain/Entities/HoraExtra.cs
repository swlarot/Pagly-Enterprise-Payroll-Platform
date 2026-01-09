using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa horas extra trabajadas por un empleado
/// </summary>
public class HoraExtra
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    /// <summary>
    /// ID del tenant al que pertenece esta hora extra
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Fecha en que se trabajaron las horas extra
    /// </summary>
    public DateTime Fecha { get; set; }

    /// <summary>
    /// Tipo de hora extra (diurna, nocturna, etc.)
    /// </summary>
    public TipoHoraExtra TipoHoraExtra { get; set; }

    /// <summary>
    /// Hora de inicio (formato TimeSpan: 18:00 = 18 horas)
    /// </summary>
    public TimeSpan HoraInicio { get; set; }

    /// <summary>
    /// Hora de finalización
    /// </summary>
    public TimeSpan HoraFin { get; set; }

    /// <summary>
    /// Cantidad de horas trabajadas (calculado automáticamente)
    /// </summary>
    public decimal CantidadHoras { get; set; }

    /// <summary>
    /// Factor multiplicador según tipo (1.25, 1.50, 1.75)
    /// </summary>
    public decimal FactorMultiplicador { get; set; }

    /// <summary>
    /// Monto calculado (se calcula en la planilla)
    /// </summary>
    public decimal? MontoCalculado { get; set; }

    /// <summary>
    /// Motivo de las horas extra
    /// </summary>
    public string Motivo { get; set; } = string.Empty;

    /// <summary>
    /// Usuario que aprobó las horas extra
    /// </summary>
    public string? AprobadoPor { get; set; }

    /// <summary>
    /// Indica si las horas extra están aprobadas
    /// </summary>
    public bool EstaAprobada { get; set; }

    /// <summary>
    /// Fecha de aprobación
    /// </summary>
    public DateTime? FechaAprobacion { get; set; }

    /// <summary>
    /// ID del detalle de planilla cuando se paga
    /// </summary>
    public int? PlanillaDetailId { get; set; }

    public string? Observaciones { get; set; }

    // Auditoría
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public virtual Tenant? Tenant { get; set; }
}
