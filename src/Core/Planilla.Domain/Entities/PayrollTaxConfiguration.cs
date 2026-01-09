// ====================================================================
// Planilla - PayrollTaxConfiguration Entity
// Source: Core360 Stage 2
// Portado: 2025-12-26
// Descripción: Configuración de tasas de CSS, Seguro Educativo e ISR
//              con effective dating para períodos escalonados (Ley 462)
// ====================================================================

using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Configuración de tasas e impuestos de planilla con vigencia temporal.
/// Maneja tasas escalonadas de CSS según Ley 462 de Panamá.
/// </summary>
public class PayrollTaxConfiguration
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// ID del tenant al que pertenece esta configuración
    /// </summary>
    [Required]
    public int TenantId { get; set; }

    /// <summary>
    /// Fecha de inicio de vigencia de esta configuración
    /// </summary>
    [Required]
    public DateTime EffectiveStartDate { get; set; }

    /// <summary>
    /// Fecha de fin de vigencia. Null = configuración vigente actual
    /// </summary>
    public DateTime? EffectiveEndDate { get; set; }

    /// <summary>
    /// Descripción del período (ej: "Período escalonamiento CSS - Fase 1")
    /// </summary>
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    // ========== TASAS CSS (Caja de Seguro Social) ==========

    /// <summary>
    /// Tasa de aporte CSS del empleado (9.75%)
    /// </summary>
    [Required]
    [Range(0, 100)]
    public decimal CssEmployeeRate { get; set; }

    /// <summary>
    /// Tasa base de aporte CSS del empleador.
    /// Escalonada según Ley 462: 13.25% → 14.25% → 15.25%
    /// </summary>
    [Required]
    [Range(0, 100)]
    public decimal CssEmployerBaseRate { get; set; }

    /// <summary>
    /// Tasa de riesgo profesional bajo (0.56%)
    /// </summary>
    [Required]
    [Range(0, 100)]
    public decimal CssRiskRateLow { get; set; }

    /// <summary>
    /// Tasa de riesgo profesional medio (2.50%)
    /// </summary>
    [Required]
    [Range(0, 100)]
    public decimal CssRiskRateMedium { get; set; }

    /// <summary>
    /// Tasa de riesgo profesional alto (5.39%)
    /// </summary>
    [Required]
    [Range(0, 100)]
    public decimal CssRiskRateHigh { get; set; }

    // ========== TOPES CSS (según años cotizados y salario promedio) ==========

    /// <summary>
    /// Tope estándar de base de cotización CSS (B/. 1,500)
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal CssMaxContributionBaseStandard { get; set; }

    /// <summary>
    /// Tope intermedio de base de cotización CSS (B/. 2,000)
    /// Requiere 25+ años cotizados y promedio salarial >= 2,000
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal CssMaxContributionBaseIntermediate { get; set; }

    /// <summary>
    /// Tope alto de base de cotización CSS (B/. 2,500)
    /// Requiere 30+ años cotizados y promedio salarial >= 2,500
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal CssMaxContributionBaseHigh { get; set; }

    /// <summary>
    /// Años mínimos cotizados para tope intermedio (25)
    /// </summary>
    [Required]
    [Range(0, int.MaxValue)]
    public int CssIntermediateMinYears { get; set; }

    /// <summary>
    /// Salario promedio mínimo para tope intermedio (B/. 2,000)
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal CssIntermediateMinAvgSalary { get; set; }

    /// <summary>
    /// Años mínimos cotizados para tope alto (30)
    /// </summary>
    [Required]
    [Range(0, int.MaxValue)]
    public int CssHighMinYears { get; set; }

    /// <summary>
    /// Salario promedio mínimo para tope alto (B/. 2,500)
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal CssHighMinAvgSalary { get; set; }

    // ========== TASAS SEGURO EDUCATIVO ==========

    /// <summary>
    /// Tasa Seguro Educativo del empleado (1.25%)
    /// IMPORTANTE: Seguro Educativo NO tiene tope máximo
    /// </summary>
    [Required]
    [Range(0, 100)]
    public decimal EducationalInsuranceEmployeeRate { get; set; }

    /// <summary>
    /// Tasa Seguro Educativo del empleador (1.50%)
    /// IMPORTANTE: Seguro Educativo NO tiene tope máximo
    /// </summary>
    [Required]
    [Range(0, 100)]
    public decimal EducationalInsuranceEmployerRate { get; set; }

    // ========== DEDUCCIONES ISR ==========

    /// <summary>
    /// Monto de deducción por dependiente para ISR (B/. 800)
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal DependentDeductionAmount { get; set; }

    /// <summary>
    /// Número máximo de dependientes permitidos para deducción ISR (3)
    /// </summary>
    [Required]
    [Range(0, int.MaxValue)]
    public int MaxDependents { get; set; }

    /// <summary>
    /// Indica si esta configuración está activa
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
