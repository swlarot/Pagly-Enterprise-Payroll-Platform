using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Registra cada deduccion aplicada en un PayrollDetail con auditoria completa.
/// Permite rastrear exactamente cuanto se solicito, cuanto se aplico, y por que se limito.
/// </summary>
public class DeduccionAplicada : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>FK al detalle de planilla donde se aplico</summary>
    public int PayrollDetailId { get; set; }

    /// <summary>FK a DeduccionFija (null para prestamos/anticipos)</summary>
    public int? DeduccionFijaId { get; set; }

    /// <summary>FK a Prestamo (null si es deduccion fija o anticipo)</summary>
    public int? PrestamoId { get; set; }

    /// <summary>FK a Anticipo (null si no es anticipo)</summary>
    public int? AnticipoId { get; set; }

    /// <summary>Tipo de deduccion aplicada</summary>
    public TipoDeduccion TipoDeduccion { get; set; }

    /// <summary>Categoria de prelacion usada</summary>
    public CategoriaDeduccion Categoria { get; set; }

    /// <summary>Descripcion de la deduccion</summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Lo que debia descontarse sin limites</summary>
    public decimal MontoSolicitado { get; set; }

    /// <summary>Lo que realmente se desconto</summary>
    public decimal MontoAplicado { get; set; }

    /// <summary>Diferencia = MontoSolicitado - MontoAplicado</summary>
    public decimal MontoLimitado { get; set; }

    /// <summary>Razon de la limitacion (ej: "Proteccion salario minimo")</summary>
    public string? RazonLimitacion { get; set; }

    /// <summary>Saldo disponible antes de aplicar esta deduccion</summary>
    public decimal SaldoDisponibleAntes { get; set; }

    /// <summary>Saldo disponible despues de aplicar esta deduccion</summary>
    public decimal SaldoDisponibleDespues { get; set; }

    /// <summary>Secuencia en que se aplico (1, 2, 3...)</summary>
    public int OrdenAplicacion { get; set; }

    /// <summary>Nombre del acreedor (copiado para inmutabilidad)</summary>
    public string? NombreAcreedor { get; set; }

    /// <summary>Fecha de transferencia al acreedor (v1 manual)</summary>
    public DateTime? FechaTransferencia { get; set; }

    /// <summary>Referencia de la transferencia bancaria</summary>
    public string? ReferenciaTransferencia { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual PayrollDetail? PayrollDetail { get; set; }
    public virtual DeduccionFija? DeduccionFija { get; set; }
    public virtual Prestamo? Prestamo { get; set; }
    public virtual Anticipo? Anticipo { get; set; }
    public virtual Tenant? Tenant { get; set; }
}
