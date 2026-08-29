// ====================================================================
// Planilla - OvertimeFactorConfiguration Entity
// Descripción: Factor multiplicador de horas extra configurable por tenant.
//              Una fila por cada TipoHoraExtra. Si un tipo no tiene fila,
//              el sistema cae al factor legal de OvertimeClassifier.FactorBase().
//
// Los factores del Código de Trabajo (Arts. 33, 36, 48-50) son MÍNIMOS:
// el empleador puede pagar por encima. Por decisión de producto el campo
// admite cualquier valor; la UI muestra el mínimo legal como referencia.
// ====================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Factor multiplicador de horas extra definido por el tenant para un tipo concreto.
/// </summary>
public class OvertimeFactorConfiguration : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>ID del tenant dueño de esta configuración.</summary>
    [Required]
    public int TenantId { get; set; }

    /// <summary>
    /// Tipo de hora extra al que aplica el factor.
    /// Único por tenant (índice compuesto TenantId + Tipo).
    /// </summary>
    [Required]
    public TipoHoraExtra Tipo { get; set; }

    /// <summary>
    /// Factor multiplicador sobre la tarifa horaria ordinaria.
    /// Incluye el 100% del salario más el recargo (ej. 1.50 = domingo Art. 48).
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(6, 4)")]
    public decimal Factor { get; set; }

    /// <summary>
    /// Recargo adicional por exceso de límites (Art. 36.4: &gt;3h/día o &gt;9h/semana).
    /// Solo se lee de la fila marcada como <see cref="EsFactorExceso"/>.
    /// </summary>
    [Column(TypeName = "decimal(6, 4)")]
    public decimal? FactorExceso { get; set; }

    /// <summary>
    /// Marca la fila que define el recargo global por exceso en lugar de un tipo puntual.
    /// </summary>
    public bool EsFactorExceso { get; set; }

    /// <summary>Permite desactivar el override sin borrar la fila (vuelve al factor legal).</summary>
    [Required]
    public bool IsActive { get; set; } = true;

    // ========== AUDITORÍA ==========

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Usuario que realizó la última modificación.</summary>
    [MaxLength(450)]
    public string? UpdatedByUserId { get; set; }

    // Navigation property
    public virtual Tenant? Tenant { get; set; }
}
