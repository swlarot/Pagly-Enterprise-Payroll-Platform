// ====================================================================
// Planilla - PayrollTaxConfigDto
// Source: Core360 Stage 2
// Portado: 2025-12-26
// Descripción: DTO de configuración de tasas para servicios de cálculo
// ====================================================================

namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// DTO de configuración de tasas e impuestos de planilla.
/// Contiene solo los campos necesarios para cálculos (sin auditoría).
/// </summary>
public record PayrollTaxConfigDto(
    int Id,
    int TenantId,
    DateTime EffectiveStartDate,
    DateTime? EffectiveEndDate,

    // Tasas CSS (Caja de Seguro Social)
    decimal CssEmployeeRate,
    decimal CssEmployerBaseRate,
    decimal CssRiskRateLow,
    decimal CssRiskRateMedium,
    decimal CssRiskRateHigh,

    // Topes CSS
    decimal CssMaxContributionBaseStandard,
    decimal CssMaxContributionBaseIntermediate,
    decimal CssMaxContributionBaseHigh,
    int CssIntermediateMinYears,
    decimal CssIntermediateMinAvgSalary,
    int CssHighMinYears,
    decimal CssHighMinAvgSalary,

    // Tasas Seguro Educativo (SIN tope máximo)
    decimal EducationalInsuranceEmployeeRate,
    decimal EducationalInsuranceEmployerRate,

    // Deducciones ISR
    decimal DependentDeductionAmount,
    int MaxDependents
);
