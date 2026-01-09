using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa un préstamo otorgado a un empleado que se descuenta en cuotas.
/// </summary>
public class Prestamo
{
    public int Id { get; set; }

    /// <summary>
    /// ID del empleado que recibe el préstamo
    /// </summary>
    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    /// <summary>
    /// ID del tenant al que pertenece este préstamo
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Descripción del préstamo (ej: "Préstamo personal", "Adelanto vacaciones")
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Monto total del préstamo otorgado
    /// </summary>
    public decimal MontoOriginal { get; set; }

    /// <summary>
    /// Monto pendiente por pagar
    /// </summary>
    public decimal MontoPendiente { get; set; }

    /// <summary>
    /// Monto de cada cuota mensual a descontar
    /// </summary>
    public decimal CuotaMensual { get; set; }

    /// <summary>
    /// Tasa de interés anual (en porcentaje, ej: 5.0 = 5%)
    /// </summary>
    public decimal TasaInteres { get; set; }

    /// <summary>
    /// Fecha de inicio del préstamo
    /// </summary>
    public DateTime FechaInicio { get; set; }

    /// <summary>
    /// Fecha estimada de finalización (puede ser null si es indefinida)
    /// </summary>
    public DateTime? FechaFin { get; set; }

    /// <summary>
    /// Número total de cuotas del préstamo
    /// </summary>
    public int NumeroCuotas { get; set; }

    /// <summary>
    /// Número de cuotas ya pagadas
    /// </summary>
    public int CuotasPagadas { get; set; }

    /// <summary>
    /// Estado actual del préstamo
    /// </summary>
    public EstadoPrestamo Estado { get; set; }

    /// <summary>
    /// Número de referencia o contrato (opcional)
    /// </summary>
    public string? Referencia { get; set; }

    /// <summary>
    /// Observaciones adicionales
    /// </summary>
    public string? Observaciones { get; set; }

    // Auditoría
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }

    // Navegación
    public ICollection<PagoPrestamo> PagosPrestamo { get; set; } = new List<PagoPrestamo>();
    public virtual Tenant? Tenant { get; set; }
}
