namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa un pago individual de una cuota de préstamo (historial).
/// </summary>
public class PagoPrestamo
{
    public int Id { get; set; }

    /// <summary>
    /// ID del préstamo al que pertenece este pago
    /// </summary>
    public int PrestamoId { get; set; }
    public Prestamo Prestamo { get; set; } = null!;

    /// <summary>
    /// ID del tenant al que pertenece este pago
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// ID del detalle de planilla donde se descontó (si aplica)
    /// </summary>
    public int? PlanillaDetailId { get; set; }

    /// <summary>
    /// Fecha en que se realizó el pago
    /// </summary>
    public DateTime FechaPago { get; set; }

    /// <summary>
    /// Monto pagado en esta cuota
    /// </summary>
    public decimal MontoPagado { get; set; }

    /// <summary>
    /// Saldo pendiente antes de este pago
    /// </summary>
    public decimal SaldoAnterior { get; set; }

    /// <summary>
    /// Saldo pendiente después de este pago
    /// </summary>
    public decimal SaldoNuevo { get; set; }

    /// <summary>
    /// Número de cuota que representa este pago (ej: 5 de 12)
    /// </summary>
    public int NumeroCuota { get; set; }

    /// <summary>
    /// Observaciones sobre este pago
    /// </summary>
    public string? Observaciones { get; set; }

    // Auditoría
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public virtual Tenant? Tenant { get; set; }
}
