using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Liquidación laboral (settlement) al terminar la relación de trabajo.
/// Cubre indemnización, preaviso, vacaciones proporcionales, décimo proporcional y salario pendiente.
/// Según el Código de Trabajo de Panamá.
/// </summary>
public class Liquidacion : ITenantEntity
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>FK al empleado liquidado</summary>
    public int EmpleadoId { get; set; }

    /// <summary>Número correlativo, ej. "LIQ-2026-001"</summary>
    [StringLength(50)]
    public string Numero { get; set; } = string.Empty;

    /// <summary>Fecha en que se genera/procesa la liquidación</summary>
    public DateTime FechaLiquidacion { get; set; }

    /// <summary>Fecha de inicio de la relación laboral</summary>
    public DateTime FechaContratacion { get; set; }

    /// <summary>Fecha efectiva de terminación</summary>
    public DateTime FechaTerminacion { get; set; }

    /// <summary>Tipo de terminación (despido, renuncia, mutuo acuerdo, etc.)</summary>
    public TipoTerminacion TipoTerminacion { get; set; }

    /// <summary>Salario base mensual del empleado al momento de la liquidación</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal SalarioBase { get; set; }

    /// <summary>Salario promedio mensual (puede incluir extras si aplica)</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal SalarioPromedio { get; set; }

    /// <summary>Años de servicio (decimal, ej: 2.5 = 2 años 6 meses)</summary>
    [Column(TypeName = "decimal(8,4)")]
    public decimal AnosServicio { get; set; }

    // ====================================================================
    // Componentes de la liquidación
    // ====================================================================

    /// <summary>Prima de antigüedad Art. 224: 1 semana/año, en toda terminación de contrato indefinido</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal PrimaAntiguedad { get; set; }

    /// <summary>Indemnización Art. 225: escala 3.4 sem/año (≤10 años) + 1 sem/año (&gt;10); solo si la causa lo dispara</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Indemnizacion { get; set; }

    /// <summary>Semanas computadas para la indemnización Art. 225</summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal IndemnizacionSemanas { get; set; }

    /// <summary>Recargo Art. 219 sobre la indemnización (reintegro ordenado y el empleador opta por pagar)</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal RecargoArt219 { get; set; }

    /// <summary>Preaviso Art. 212: 30 días de salario (cuando aplica)</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Preaviso { get; set; }

    /// <summary>Vacaciones proporcionales Art. 54</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal VacacionesProporcionales { get; set; }

    /// <summary>Días de vacaciones proporcionales calculados</summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal DiasVacacionesProporcionales { get; set; }

    /// <summary>Décimo tercer mes proporcional del tercio en curso</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal DecimoTercerMesProporcional { get; set; }

    /// <summary>Cesantía Fondo (Decreto 60/1995) para contratos DEFINIDO/POR_OBRA — sustituye la prima</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Cesantia { get; set; }

    /// <summary>Salario pendiente por días trabajados no pagados</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal SalarioPendiente { get; set; }

    /// <summary>Días de salario pendiente</summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal DiasSalarioPendiente { get; set; }

    /// <summary>Si se incluyó preaviso en el cálculo</summary>
    public bool IncluyePreaviso { get; set; }

    // ====================================================================
    // Deducciones
    // ====================================================================

    /// <summary>CSS empleado (aplica sobre vacaciones y décimo proporcional)</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal CssEmpleado { get; set; }

    /// <summary>Seguro Educativo empleado (aplica sobre vacaciones y décimo proporcional)</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal SeEmpleado { get; set; }

    /// <summary>ISR (generalmente no aplica en liquidación, pero se incluye por flexibilidad)</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Isr { get; set; }

    // ====================================================================
    // Costos patronales
    // ====================================================================

    /// <summary>CSS patronal sobre componentes sujetos</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal CssPatronal { get; set; }

    /// <summary>SE patronal sobre componentes sujetos</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal SePatronal { get; set; }

    // ====================================================================
    // Totales
    // ====================================================================

    /// <summary>Total bruto (suma de todos los componentes)</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalBruto { get; set; }

    /// <summary>Total deducciones (CSS + SE + ISR)</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDeducciones { get; set; }

    /// <summary>Total neto a pagar al empleado</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalNeto { get; set; }

    // ====================================================================
    // Estado y observaciones
    // ====================================================================

    /// <summary>Estado del flujo de trabajo</summary>
    public EstadoLiquidacion Estado { get; set; } = EstadoLiquidacion.Calculada;

    /// <summary>Observaciones o notas adicionales</summary>
    [StringLength(1000)]
    public string? Observaciones { get; set; }

    // ====================================================================
    // Auditoría
    // ====================================================================

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [StringLength(450)]
    public string? CreatedBy { get; set; }

    [StringLength(450)]
    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    // ====================================================================
    // Navegación
    // ====================================================================

    public virtual Empleado? Empleado { get; set; }
    public virtual Tenant? Tenant { get; set; }
}
