using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Horas trabajadas por un empleado en un período de planilla específico.
/// Permite ingresar manualmente las horas regulares, dominicales, feriados y extras.
/// Se utiliza para calcular el salario bruto del período.
/// </summary>
public class PayrollEmployeeHours : ITenantEntity
{
    public int Id { get; set; }

    /// <summary>ID de la planilla</summary>
    public int PayrollHeaderId { get; set; }

    /// <summary>ID del empleado</summary>
    public int EmpleadoId { get; set; }

    /// <summary>ID del tenant (multi-tenancy)</summary>
    public int TenantId { get; set; }

    // ====================================================================
    // Horas por tipo
    // ====================================================================

    /// <summary>Horas regulares trabajadas en el período</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal RegularHours { get; set; }

    /// <summary>Horas trabajadas en domingo (recargo 50%)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal SundayHours { get; set; } = 0;

    /// <summary>Horas trabajadas en días feriados nacionales (recargo 50%)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal HolidayHours { get; set; } = 0;

    /// <summary>Horas extra diurnas (recargo 25%)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal OvertimeDayHours { get; set; } = 0;

    /// <summary>Horas extra nocturnas (recargo 50%)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal OvertimeNightHours { get; set; } = 0;

    /// <summary>Horas extra en días festivos nacionales (FiestaNacionalDiurna o FiestaNacionalNocturna)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal OvertimeHolidayHours { get; set; } = 0;

    /// <summary>Horas extra mixtas (diurna-nocturna o nocturna-diurna)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal OvertimeMixedHours { get; set; } = 0;

    /// <summary>Horas extra con exceso (>3h/día o >9h/semana)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal OvertimeExcessHours { get; set; } = 0;

    /// <summary>Horas de ausencia injustificada (se descuentan)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal AbsenceHours { get; set; } = 0;

    /// <summary>Horas de incapacidad CSS (no se descuentan pero afectan cálculo)</summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal DisabilityHours { get; set; } = 0;

    /// <summary>Comisiones del período (monto en balboas, no horas)</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Commissions { get; set; } = 0;

    // ====================================================================
    // Montos calculados (se llenan al calcular planilla)
    // ====================================================================

    /// <summary>Pago por horas regulares: RegularHours × HourlyRate</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal RegularPay { get; set; }

    /// <summary>Pago por horas domingo: SundayHours × HourlyRate × 1.50</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal SundayPay { get; set; }

    /// <summary>Pago por horas feriado: HolidayHours × HourlyRate × 1.50</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal HolidayPay { get; set; }

    /// <summary>Pago horas extra diurnas: OvertimeDayHours × HourlyRate × 1.25</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal OvertimeDayPay { get; set; }

    /// <summary>Pago horas extra nocturnas: OvertimeNightHours × HourlyRate × 1.50</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal OvertimeNightPay { get; set; }

    /// <summary>Pago por horas extra en festivos: OvertimeHolidayHours × HourlyRate × factor (3.125x o 3.75x según diurna/nocturna)</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal OvertimeHolidayPay { get; set; }

    /// <summary>Pago por horas extra mixtas: OvertimeMixedHours × HourlyRate × factor (1.50x o 1.75x según tipo)</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal OvertimeMixedPay { get; set; }

    /// <summary>Pago por horas extra con exceso: OvertimeExcessHours × HourlyRate × factor base × 1.75x adicional</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal OvertimeExcessPay { get; set; }

    /// <summary>Descuento por ausencias: AbsenceHours × HourlyRate</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal AbsenceDeduction { get; set; }

    /// <summary>Total pagado por horas: Suma de todos los pagos menos descuentos</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalHoursPay { get; set; }

    // ====================================================================
    // Auditoría
    // ====================================================================
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ====================================================================
    // Navegación
    // ====================================================================
    public virtual PayrollHeader? PayrollHeader { get; set; }
    public virtual Empleado? Empleado { get; set; }
    public virtual Tenant? Tenant { get; set; }
}
