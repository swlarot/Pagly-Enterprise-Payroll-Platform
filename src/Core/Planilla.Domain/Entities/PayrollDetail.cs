// ====================================================================
// Planilla - PayrollDetail
// Source: Core360 Stage 3
// Creado: 2025-12-26
// Descripción: Detalle de planilla (cálculos por empleado)
// Cambios vs Core360:
//   - Agregado desglose de salario bruto (BaseSalary, Overtime, Bonuses, Commissions)
//   - Agregado EmployerCost total por empleado
//   - Separado CSS empleado/empleador, SE empleado/empleador
// ====================================================================

using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Detalle de planilla que representa el cálculo de nómina de un empleado en un período.
/// Almacena todos los ingresos, deducciones y costos patronales calculados.
/// </summary>
public class PayrollDetail
{
    /// <summary>
    /// ID único del detalle.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ID de la planilla (encabezado) a la que pertenece este detalle.
    /// </summary>
    public int PayrollHeaderId { get; set; }

    /// <summary>
    /// ID del empleado al que corresponde este cálculo.
    /// </summary>
    public int EmpleadoId { get; set; }

    /// <summary>
    /// ID del tenant al que pertenece este detalle de planilla
    /// </summary>
    public int TenantId { get; set; }

    // ====================================================================
    // Ingresos (componentes del salario bruto)
    // ====================================================================

    /// <summary>
    /// Salario bruto total del empleado en el período.
    /// Suma de: BaseSalary + OvertimePay + Bonuses + Commissions.
    /// </summary>
    public decimal GrossPay { get; set; }

    /// <summary>
    /// Salario base del empleado (salario regular del período).
    /// </summary>
    public decimal BaseSalary { get; set; }

    /// <summary>
    /// Pago por horas extras.
    /// </summary>
    public decimal OvertimePay { get; set; }

    /// <summary>
    /// Bonificaciones y aguinaldos del período.
    /// </summary>
    public decimal Bonuses { get; set; }

    /// <summary>
    /// Comisiones por ventas u otros conceptos.
    /// </summary>
    public decimal Commissions { get; set; }

    // ====================================================================
    // Deducciones: CSS (Caja de Seguro Social)
    // ====================================================================

    /// <summary>
    /// Aporte CSS del empleado (deducción de nómina).
    /// Tasa fija: 9.75% sobre base topada.
    /// </summary>
    public decimal CssEmployee { get; set; }

    /// <summary>
    /// Aporte CSS del empleador (costo patronal).
    /// Tasa escalonada: 13.25% / 14.25% / 15.25% según período.
    /// </summary>
    public decimal CssEmployer { get; set; }

    /// <summary>
    /// Aporte de riesgo profesional (costo patronal).
    /// Tasa variable: 0.56% / 2.50% / 5.39% según nivel de riesgo.
    /// </summary>
    public decimal RiskContribution { get; set; }

    // ====================================================================
    // Deducciones: Seguro Educativo (SE)
    // ====================================================================

    /// <summary>
    /// Seguro Educativo del empleado (deducción de nómina).
    /// Tasa: 1.25% sobre salario completo (SIN tope).
    /// </summary>
    public decimal EducationalInsuranceEmployee { get; set; }

    /// <summary>
    /// Seguro Educativo del empleador (costo patronal).
    /// Tasa: 1.50% sobre salario completo (SIN tope).
    /// </summary>
    public decimal EducationalInsuranceEmployer { get; set; }

    // ====================================================================
    // Deducciones: Impuesto Sobre la Renta (ISR)
    // ====================================================================

    /// <summary>
    /// Impuesto Sobre la Renta (deducción de nómina).
    /// Brackets progresivos anuales: 0% / 15% / 25%.
    /// </summary>
    public decimal IncomeTax { get; set; }

    // ====================================================================
    // Deducciones: Otras
    // ====================================================================

    /// <summary>
    /// Otras deducciones (préstamos, embargos, seguros privados, etc.).
    /// </summary>
    public decimal OtherDeductions { get; set; }

    /// <summary>
    /// Suma de deducciones fijas aplicables en este período
    /// </summary>
    public decimal DeduccionesFijas { get; set; }

    /// <summary>
    /// Suma de cuotas de préstamos descontadas en este período
    /// </summary>
    public decimal Prestamos { get; set; }

    /// <summary>
    /// Suma de anticipos descontados en este período
    /// </summary>
    public decimal Anticipos { get; set; }

    /// <summary>
    /// Desglose detallado de otras deducciones en formato JSON (opcional)
    /// </summary>
    public string? OtrasDeduccionesDetalle { get; set; }

    // ====================================================================
    // Asistencia: Horas Extra
    // ====================================================================

    /// <summary>
    /// Horas extra diurnas trabajadas
    /// </summary>
    public decimal HorasExtraDiurnas { get; set; }

    /// <summary>
    /// Horas extra nocturnas trabajadas
    /// </summary>
    public decimal HorasExtraNocturnas { get; set; }

    /// <summary>
    /// Horas extra en domingo o feriado
    /// </summary>
    public decimal HorasExtraDomingoFeriado { get; set; }

    /// <summary>
    /// Monto total por horas extra
    /// </summary>
    public decimal MontoHorasExtra { get; set; }

    // ====================================================================
    // Asistencia: Ausencias
    // ====================================================================

    /// <summary>
    /// Días de ausencia injustificada
    /// </summary>
    public decimal DiasAusenciaInjustificada { get; set; }

    /// <summary>
    /// Monto descontado por ausencias
    /// </summary>
    public decimal MontoDescuentoAusencias { get; set; }

    // ====================================================================
    // Asistencia: Vacaciones
    // ====================================================================

    /// <summary>
    /// Días de vacaciones tomados en el período
    /// </summary>
    public decimal DiasVacaciones { get; set; }

    /// <summary>
    /// Monto pagado por vacaciones
    /// </summary>
    public decimal MontoVacaciones { get; set; }

    // ====================================================================
    // Totales calculados
    // ====================================================================

    /// <summary>
    /// Total de deducciones al empleado.
    /// Suma de: CssEmployee + EducationalInsuranceEmployee + IncomeTax + OtherDeductions + DeduccionesFijas + Prestamos + Anticipos.
    /// </summary>
    public decimal TotalDeductions { get; set; }

    /// <summary>
    /// Salario neto a pagar al empleado (GrossPay - TotalDeductions).
    /// </summary>
    public decimal NetPay { get; set; }

    /// <summary>
    /// Costo total del empleador para este empleado.
    /// Suma de: CssEmployer + EducationalInsuranceEmployer + RiskContribution.
    /// </summary>
    public decimal EmployerCost { get; set; }

    // ====================================================================
    // Auditoría
    // ====================================================================

    /// <summary>
    /// Fecha de creación del registro.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha de última actualización del registro.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // ====================================================================
    // Relaciones
    // ====================================================================

    /// <summary>
    /// Encabezado de planilla al que pertenece este detalle.
    /// </summary>
    public virtual PayrollHeader? PayrollHeader { get; set; }

    /// <summary>
    /// Empleado al que corresponde este cálculo.
    /// </summary>
    public virtual Empleado? Empleado { get; set; }

    /// <summary>
    /// Tenant al que pertenece este detalle
    /// </summary>
    public virtual Tenant? Tenant { get; set; }
}
