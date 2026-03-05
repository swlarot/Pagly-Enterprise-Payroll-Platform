using System.ComponentModel.DataAnnotations.Schema;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Cabecera de una planilla de Décimo Tercer Mes (Ley 29 de 1976).
/// Cubre una de las 3 partidas anuales: abril, agosto o diciembre.
/// </summary>
public class PlanillaDecimo : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Número correlativo, ej. "DEC-2026-01"</summary>
    public string Numero { get; set; } = string.Empty;

    /// <summary>Inicio del período de devengamiento (ej. 16-dic)</summary>
    public DateTime PeriodoDesde { get; set; }

    /// <summary>Fin del período de devengamiento (ej. 15-abr)</summary>
    public DateTime PeriodoHasta { get; set; }

    /// <summary>Fecha de pago legal (15 abril, 15 agosto o 15 diciembre)</summary>
    public DateTime FechaPago { get; set; }

    public EstadoDecimo Estado { get; set; } = EstadoDecimo.Borrador;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDevengado { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDecimo { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCssEmpleado { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCssPatrono { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalSeEmpleado { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalSePatrono { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalISR { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalNetoPago { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public virtual Tenant? Tenant { get; set; }
    public virtual List<DetalleDecimo> Detalles { get; set; } = [];
}
