using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa una deducción fija que se aplica automáticamente al empleado en cada planilla.
/// </summary>
public class DeduccionFija
{
    public int Id { get; set; }

    /// <summary>
    /// ID del empleado al que se aplica la deducción
    /// </summary>
    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    /// <summary>
    /// ID del tenant al que pertenece esta deducción
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Tipo de deducción
    /// </summary>
    public TipoDeduccion TipoDeduccion { get; set; }

    /// <summary>
    /// Descripción de la deducción (ej: "Pensión alimenticia - Exp. 123-2024")
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Monto fijo por período (si no es porcentaje)
    /// </summary>
    public decimal Monto { get; set; }

    /// <summary>
    /// Porcentaje sobre salario bruto (alternativa a monto fijo)
    /// </summary>
    public decimal? Porcentaje { get; set; }

    /// <summary>
    /// Indica si la deducción se calcula como porcentaje del salario
    /// </summary>
    public bool EsPorcentaje { get; set; }

    /// <summary>
    /// Fecha de inicio de la deducción
    /// </summary>
    public DateTime FechaInicio { get; set; }

    /// <summary>
    /// Fecha de fin de la deducción (null = indefinida)
    /// </summary>
    public DateTime? FechaFin { get; set; }

    /// <summary>
    /// Indica si la deducción está activa
    /// </summary>
    public bool EstaActivo { get; set; }

    /// <summary>
    /// Número de referencia (expediente, contrato, etc.)
    /// </summary>
    public string? Referencia { get; set; }

    /// <summary>
    /// Prioridad de aplicación (menor número = mayor prioridad)
    /// Útil cuando hay múltiples deducciones
    /// </summary>
    public int Prioridad { get; set; }

    /// <summary>
    /// Observaciones adicionales
    /// </summary>
    public string? Observaciones { get; set; }

    // Auditoría
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public virtual Tenant? Tenant { get; set; }
}
