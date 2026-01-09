using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa una solicitud de vacaciones de un empleado
/// </summary>
public class SolicitudVacaciones
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    /// <summary>
    /// ID del tenant al que pertenece esta solicitud
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Fecha de inicio de las vacaciones
    /// </summary>
    public DateTime FechaInicio { get; set; }

    /// <summary>
    /// Fecha de fin de las vacaciones
    /// </summary>
    public DateTime FechaFin { get; set; }

    /// <summary>
    /// Días de vacaciones solicitados
    /// </summary>
    public int DiasVacaciones { get; set; }

    /// <summary>
    /// Días proporcionales disponibles al momento de la solicitud
    /// </summary>
    public decimal DiasProporcionales { get; set; }

    /// <summary>
    /// Estado de la solicitud
    /// </summary>
    public EstadoVacaciones Estado { get; set; }

    /// <summary>
    /// Fecha en que se creó la solicitud
    /// </summary>
    public DateTime FechaSolicitud { get; set; }

    /// <summary>
    /// Usuario que aprobó la solicitud
    /// </summary>
    public string? AprobadoPor { get; set; }

    /// <summary>
    /// Fecha de aprobación
    /// </summary>
    public DateTime? FechaAprobacion { get; set; }

    /// <summary>
    /// Usuario que rechazó la solicitud
    /// </summary>
    public string? RechazadoPor { get; set; }

    /// <summary>
    /// Fecha de rechazo
    /// </summary>
    public DateTime? FechaRechazo { get; set; }

    /// <summary>
    /// Motivo del rechazo
    /// </summary>
    public string? MotivoRechazo { get; set; }

    /// <summary>
    /// ID del detalle de planilla cuando se pagan las vacaciones
    /// </summary>
    public int? PlanillaDetailId { get; set; }

    public string? Observaciones { get; set; }

    // Auditoría
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public virtual Tenant? Tenant { get; set; }
}
