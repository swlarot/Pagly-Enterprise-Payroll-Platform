// ====================================================================
// Planilla - TaxBracket Entity
// Source: Core360 Stage 2
// Portado: 2025-12-26
// Descripción: Tramos de Impuesto Sobre la Renta (ISR) progresivos
//              según regulaciones de la DGI de Panamá
// ====================================================================

using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa un tramo del Impuesto Sobre la Renta (ISR) con su rango de ingresos
/// y tasa aplicable. El ISR en Panamá es progresivo por tramos.
/// </summary>
public class TaxBracket
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// ID del tenant al que pertenece este bracket
    /// </summary>
    [Required]
    public int TenantId { get; set; }

    /// <summary>
    /// Año fiscal al que aplica este bracket (ej: 2025)
    /// </summary>
    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }

    /// <summary>
    /// Orden de aplicación del bracket (1 = primer tramo, 2 = segundo, etc.)
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int Order { get; set; }

    /// <summary>
    /// Descripción del tramo (ej: "Tramo exento", "Tramo 15%", "Tramo 25%")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Ingreso mínimo anual para este tramo (inclusive)
    /// Ejemplo: Tramo exento inicia en 0.00
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal MinIncome { get; set; }

    /// <summary>
    /// Ingreso máximo anual para este tramo (inclusive).
    /// Null = sin tope superior (último tramo)
    /// Ejemplo: Tramo 15% va de 11,000.01 a 50,000.00
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? MaxIncome { get; set; }

    /// <summary>
    /// Tasa de impuesto aplicable a este tramo (porcentaje)
    /// Ejemplo: 0 (exento), 15, 25
    /// </summary>
    [Required]
    [Range(0, 100)]
    public decimal Rate { get; set; }

    /// <summary>
    /// Monto fijo de impuesto acumulado de brackets anteriores.
    /// Se suma al cálculo del excedente en este tramo.
    /// Ejemplo: Tramo 25% tiene fixedAmount = 5,850 (impuesto de tramos previos)
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal FixedAmount { get; set; }

    /// <summary>
    /// Indica si este bracket está activo
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = true;

    // ========== CAMPOS DE AUDITORÍA ==========

    /// <summary>
    /// Fecha de creación del registro
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha de última actualización del registro
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public virtual Tenant? Tenant { get; set; }
}
