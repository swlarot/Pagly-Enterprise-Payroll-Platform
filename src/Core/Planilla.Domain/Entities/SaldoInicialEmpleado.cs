using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Saldo inicial de un empleado al momento de migrar al sistema.
/// Permite capturar vacaciones acumuladas y décimo acumulado previos al uso del sistema,
/// para que los cálculos de primer año sean correctos.
/// </summary>
public class SaldoInicialEmpleado : ITenantEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    /// <summary>ID del empleado al que corresponde este saldo</summary>
    public int EmpleadoId { get; set; }

    /// <summary>Fecha de corte a partir de la cual el sistema toma el control (inicio en el sistema)</summary>
    public DateTime FechaCorte { get; set; }

    // ====================================================================
    // Vacaciones
    // ====================================================================

    /// <summary>Días de vacaciones acumulados antes de migrar al sistema</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal DiasVacacionesAcumulados { get; set; } = 0;

    /// <summary>Días de vacaciones ya tomados antes de migrar al sistema (de los acumulados)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal DiasVacacionesTomados { get; set; } = 0;

    // ====================================================================
    // Décimo Tercer Mes
    // ====================================================================

    /// <summary>
    /// Monto de décimo tercer mes acumulado antes de migrar al sistema.
    /// Se suma al primer cálculo de décimo del año.
    /// </summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal MontoDecimoAcumulado { get; set; } = 0;

    /// <summary>Notas o contexto sobre el origen de los saldos</summary>
    [MaxLength(500)]
    public string? Notas { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ====================================================================
    // Navegación
    // ====================================================================
    public virtual Empleado? Empleado { get; set; }
    public virtual Tenant? Tenant { get; set; }
}
