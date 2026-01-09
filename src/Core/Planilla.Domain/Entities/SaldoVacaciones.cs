namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa el saldo de días de vacaciones de un empleado
/// </summary>
public class SaldoVacaciones
{
    public int Id { get; set; }

    /// <summary>
    /// ID del empleado (único - un empleado solo tiene un saldo)
    /// </summary>
    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    /// <summary>
    /// ID del tenant al que pertenece este saldo
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Días de vacaciones acumulados
    /// </summary>
    public decimal DiasAcumulados { get; set; }

    /// <summary>
    /// Días de vacaciones ya tomados
    /// </summary>
    public decimal DiasTomados { get; set; }

    /// <summary>
    /// Días disponibles (calculado: Acumulados - Tomados)
    /// </summary>
    public decimal DiasDisponibles { get; set; }

    /// <summary>
    /// Fecha de última actualización del saldo
    /// </summary>
    public DateTime UltimaActualizacion { get; set; }

    /// <summary>
    /// Inicio del año fiscal de vacaciones
    /// </summary>
    public DateTime PeriodoInicio { get; set; }

    /// <summary>
    /// Fin del año fiscal de vacaciones
    /// </summary>
    public DateTime PeriodoFin { get; set; }

    // Auditoría
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public virtual Tenant? Tenant { get; set; }
}
