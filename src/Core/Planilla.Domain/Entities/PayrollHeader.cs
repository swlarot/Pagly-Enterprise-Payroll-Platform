// ====================================================================
// Planilla - PayrollHeader
// Source: Core360 Stage 3
// Creado: 2025-12-26
// Descripción: Encabezado de planilla (período de pago)
// Cambios vs Core360:
//   - Agregado RowVersion para control de concurrencia
//   - Agregado PayrollNumber (identificador único alfanumérico)
//   - Separado aprobación y pago en campos distintos
// ====================================================================

using System.ComponentModel.DataAnnotations;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Encabezado de planilla que representa un período de pago completo.
/// Una planilla agrupa múltiples PayrollDetail (uno por empleado).
/// </summary>
public class PayrollHeader
{
    /// <summary>
    /// ID único de la planilla.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ID del tenant al que pertenece esta planilla (multi-tenancy)
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Número de planilla único (ej: "PL-2025-01", "QUIN-2025-02").
    /// Alfanumérico, generado automáticamente.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string PayrollNumber { get; set; } = string.Empty;

    /// <summary>
    /// Fecha de inicio del período de pago.
    /// </summary>
    public DateTime PeriodStartDate { get; set; }

    /// <summary>
    /// Fecha de fin del período de pago.
    /// </summary>
    public DateTime PeriodEndDate { get; set; }

    /// <summary>
    /// Fecha programada de pago a empleados.
    /// </summary>
    public DateTime PayDate { get; set; }

    /// <summary>
    /// Estado actual de la planilla en el workflow.
    /// </summary>
    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;

    // ====================================================================
    // Totales calculados (suma de todos los PayrollDetail)
    // ====================================================================

    /// <summary>
    /// Total de salarios brutos de la planilla.
    /// </summary>
    public decimal TotalGrossPay { get; set; }

    /// <summary>
    /// Total de deducciones (CSS + SE + ISR + otras) de la planilla.
    /// </summary>
    public decimal TotalDeductions { get; set; }

    /// <summary>
    /// Total neto a pagar a empleados (TotalGrossPay - TotalDeductions).
    /// </summary>
    public decimal TotalNetPay { get; set; }

    /// <summary>
    /// Total de costos patronales (CSS empleador + SE empleador + Riesgo).
    /// </summary>
    public decimal TotalEmployerCost { get; set; }

    // ====================================================================
    // Campos de workflow: Calculated
    // ====================================================================

    /// <summary>
    /// Fecha en que se calculó la planilla (transición Draft → Calculated).
    /// </summary>
    public DateTime? ProcessedDate { get; set; }

    /// <summary>
    /// Usuario que procesó/calculó la planilla.
    /// </summary>
    [MaxLength(256)]
    public string? ProcessedBy { get; set; }

    // ====================================================================
    // Campos de workflow: Approved
    // ====================================================================

    /// <summary>
    /// Indica si la planilla fue aprobada.
    /// </summary>
    public bool IsApproved { get; set; }

    /// <summary>
    /// Fecha de aprobación de la planilla.
    /// </summary>
    public DateTime? ApprovedDate { get; set; }

    /// <summary>
    /// Usuario que aprobó la planilla.
    /// </summary>
    [MaxLength(256)]
    public string? ApprovedBy { get; set; }

    // ====================================================================
    // Campos de workflow: Paid
    // ====================================================================

    /// <summary>
    /// Indica si la planilla fue pagada.
    /// </summary>
    public bool IsPaid { get; set; }

    /// <summary>
    /// Fecha efectiva de pago.
    /// </summary>
    public DateTime? PaidDate { get; set; }

    /// <summary>
    /// Referencia del pago (número de lote bancario, ID de transferencia, etc.).
    /// </summary>
    [MaxLength(100)]
    public string? PaymentReference { get; set; }

    // ====================================================================
    // Concurrencia optimista
    // ====================================================================
    // PostgreSQL usa xmin (shadow property manejada por EF Core)

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
    /// Detalles de la planilla (uno por empleado).
    /// </summary>
    public virtual ICollection<PayrollDetail> Details { get; set; } = new List<PayrollDetail>();

    /// <summary>
    /// Navigation property para Tenant
    /// </summary>
    public virtual Tenant? Tenant { get; set; }
}
