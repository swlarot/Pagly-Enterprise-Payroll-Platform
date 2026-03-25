using System.ComponentModel.DataAnnotations.Schema;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Detalle por empleado de una planilla de Décimo Tercer Mes.
/// Incluye desglose mensual (JSON), cálculos CSS/SE/ISR y neto a pagar.
/// Tasas CSS especiales: empleado 7.25%, patrono 10.75% (vs 9.75%/12.25% regular).
/// </summary>
public class DetalleDecimo : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int PlanillaDecimoId { get; set; }
    public int EmpleadoId { get; set; }

    /// <summary>
    /// JSON con el desglose mes a mes: [{ano:2025, mes:12, bruto:3000.00}, ...]
    /// </summary>
    public string DesgloseMensualJson { get; set; } = "[]";

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDevengado { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MontoDecimo { get; set; }

    /// <summary>CSS empleado al 7.25% sobre MontoDecimo</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal CssEmpleado { get; set; }

    /// <summary>CSS patrono al 10.75% sobre MontoDecimo</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal CssPatrono { get; set; }

    /// <summary>Seguro Educativo empleado al 1.25% sobre MontoDecimo</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal SeEmpleado { get; set; }

    /// <summary>Seguro Educativo patrono al 1.50% sobre MontoDecimo</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal SePatrono { get; set; }

    /// <summary>ISR del período: (salario mensual equiv. + monto décimo) × 13 → tramos → / 13</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal ISR { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDeducciones { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetoPago { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual PlanillaDecimo? PlanillaDecimo { get; set; }
    public virtual Empleado? Empleado { get; set; }
    public virtual Tenant? Tenant { get; set; }
}
