// ====================================================================
// Planilla - AcumuladoFiscalEmpleado
// Lo que un empleado lleva acumulado en un año fiscal para efectos de ISR.
//
// Es la pieza que hace posible el método acumulativo: en cada corrida el motor
// compara el impuesto que debería llevar retenido contra el que ya se le retuvo,
// y cobra solo la diferencia. Sin este registro no hay contra qué comparar.
//
// Separa lo cargado al migrar desde otro sistema de lo generado por esta
// plataforma, para que una migración a mitad de año no duplique retenciones.
// ====================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>Acumulado fiscal de un empleado dentro de un año fiscal.</summary>
public class AcumuladoFiscalEmpleado : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int TenantId { get; set; }

    [Required]
    public int EmpleadoId { get; set; }

    /// <summary>Año fiscal al que pertenece el acumulado.</summary>
    [Required]
    public int Anio { get; set; }

    // ========== SALDOS INICIALES (migración desde otro sistema) ==========

    /// <summary>Ingreso gravable que el empleado ya traía acumulado al migrar.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal IngresoGravableInicial { get; set; }

    /// <summary>Décimo tercer mes ya acumulado al migrar.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal DecimoInicial { get; set; }

    /// <summary>
    /// ISR ya retenido al empleado antes de entrar a este sistema.
    /// Se resta del impuesto debido acumulado para no cobrarle dos veces.
    /// </summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal IsrRetenidoInicial { get; set; }

    // ========== ACUMULADOS GENERADOS POR EL SISTEMA ==========

    [Column(TypeName = "decimal(18, 2)")]
    public decimal IngresoGravableProcesado { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal DecimoProcesado { get; set; }

    /// <summary>ISR retenido en las planillas regulares.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal IsrRegularProcesado { get; set; }

    /// <summary>ISR retenido en las corridas de décimo, separado para poder conciliarlo.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal IsrDecimoProcesado { get; set; }

    /// <summary>
    /// Exceso retenido que queda a favor del empleado. Se conserva para conciliación:
    /// nunca se muestra como un valor negativo en el comprobante de pago.
    /// </summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal SaldoFavorEmpleado { get; set; }

    /// <summary>Número de corridas del empleado en el año. Es el divisor de la proyección.</summary>
    public int PeriodosProcesados { get; set; }

    // ========== CALCULADOS ==========

    [NotMapped]
    public decimal IngresoGravableTotal => IngresoGravableInicial + IngresoGravableProcesado;

    [NotMapped]
    public decimal DecimoTotal => DecimoInicial + DecimoProcesado;

    [NotMapped]
    public decimal IsrRetenidoTotal => IsrRetenidoInicial + IsrRegularProcesado + IsrDecimoProcesado;

    // ========== AUDITORÍA ==========

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navegación
    public virtual Empleado? Empleado { get; set; }
    public virtual Tenant? Tenant { get; set; }
}
