// ====================================================================
// Planilla - DefaultPanamaPayrollConfig
// Descripción: Configuración estática de planilla Panamá (Ley 462, 3 fases)
//              para uso en escenarios stateless (API Platform B2B).
//              NO depende de ApplicationDbContext ni de ningún tenant.
//
// Fuente de verdad: PayrollConfigSeeder.cs (mismos valores legales).
// Si cambian las constantes en el seeder, actualizar aquí también.
//
// Regla de oro: ningún método de esta clase debe hacer I/O. Todo es
// data estática compilada. Un test de paridad debe garantizar que los
// valores coinciden con el seed de BD para el tenant plantilla.
// ====================================================================

using Vorluno.Planilla.Application.DTOs;

namespace Vorluno.Planilla.Application.Configuration;

/// <summary>
/// Configuración estática de planilla para Panamá.
/// Expone la configuración vigente para cualquier fecha mediante
/// lookup en memoria de las 3 fases de la Reforma CSS (Ley 462).
/// </summary>
public static class DefaultPanamaPayrollConfig
{
    /// <summary>
    /// Versión del set de constantes. Incrementar cuando cambien reglas legales.
    /// Formato: YYYY.REV — 2026.1 = primera revisión vigente en 2026.
    /// </summary>
    public const string Version = "2026.1";

    // ====================================================================
    // Reforma CSS — Ley 462 (3 fases escalonadas)
    // ====================================================================

    private static readonly DateTime Phase1Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Phase1End = new(2027, 2, 28, 23, 59, 59, DateTimeKind.Utc);

    private static readonly DateTime Phase2Start = new(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Phase2End = new(2029, 2, 28, 23, 59, 59, DateTimeKind.Utc);

    private static readonly DateTime Phase3Start = new(2029, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    // ====================================================================
    // Configuración común a las 3 fases
    // ====================================================================

    // CSS empleado: fija 9.75% en las 3 fases
    private const decimal CssEmployeeRate = 9.75m;

    // Tasas de riesgo profesional (Acuerdo N°2 de 1995)
    private const decimal CssRiskRateLow = 0.56m;        // Clase I
    private const decimal CssRiskRateMedium = 2.10m;     // Clase III
    private const decimal CssRiskRateHigh = 5.67m;       // Clase V

    // Topes de pensión Art. 178 Ley 462 (NO son topes de cotización)
    private const decimal CssMaxContributionBaseStandard = 1500.00m;
    private const decimal CssMaxContributionBaseIntermediate = 2000.00m;
    private const decimal CssMaxContributionBaseHigh = 2500.00m;

    // Requisitos para topes superiores
    private const int CssIntermediateMinYears = 25;
    private const decimal CssIntermediateMinAvgSalary = 2000.00m;
    private const int CssHighMinYears = 30;
    private const decimal CssHighMinAvgSalary = 2500.00m;

    // Seguro Educativo
    private const decimal EducationalInsuranceEmployeeRate = 1.25m;
    private const decimal EducationalInsuranceEmployerRate = 1.50m;

    // ISR
    private const decimal DependentDeductionAmount = 800.00m;
    private const int MaxDependents = 99; // Ley 37/2018 + Decreto 368/2018 — sin límite legal

    // DEV-28: Salario mínimo legal (referencial, varía por sector)
    private const decimal SalarioMinimoLegal = 1.30m; // B/. por hora (referencia comercio — sector más común)

    // ====================================================================
    // API pública
    // ====================================================================

    /// <summary>
    /// Retorna la configuración vigente para una fecha específica.
    /// El TenantId del DTO se devuelve como el valor que el caller pase
    /// (típicamente el tenantId del key usado) para preservar trazabilidad.
    /// </summary>
    public static PayrollTaxConfigDto GetConfigForDate(DateTime effectiveDate, int tenantIdForDto = 0)
    {
        var (start, end, employerRate, description) = DetermineFase(effectiveDate);

        return new PayrollTaxConfigDto(
            Id: 0, // Static config no tiene Id de BD
            TenantId: tenantIdForDto,
            EffectiveStartDate: start,
            EffectiveEndDate: end,

            // CSS
            CssEmployeeRate: CssEmployeeRate,
            CssEmployerBaseRate: employerRate,
            CssRiskRateLow: CssRiskRateLow,
            CssRiskRateMedium: CssRiskRateMedium,
            CssRiskRateHigh: CssRiskRateHigh,

            // Topes CSS
            CssMaxContributionBaseStandard: CssMaxContributionBaseStandard,
            CssMaxContributionBaseIntermediate: CssMaxContributionBaseIntermediate,
            CssMaxContributionBaseHigh: CssMaxContributionBaseHigh,
            CssIntermediateMinYears: CssIntermediateMinYears,
            CssIntermediateMinAvgSalary: CssIntermediateMinAvgSalary,
            CssHighMinYears: CssHighMinYears,
            CssHighMinAvgSalary: CssHighMinAvgSalary,

            // Seguro Educativo
            EducationalInsuranceEmployeeRate: EducationalInsuranceEmployeeRate,
            EducationalInsuranceEmployerRate: EducationalInsuranceEmployerRate,

            // ISR
            DependentDeductionAmount: DependentDeductionAmount,
            MaxDependents: MaxDependents,

            SalarioMinimoLegal: SalarioMinimoLegal
        );
    }

    /// <summary>
    /// Retorna los brackets ISR para un año fiscal.
    /// Para años no cubiertos explícitamente (ej: 2028+), retorna los brackets 2026
    /// porque las leyes de ISR no han cambiado. Cuando cambien, actualizar esta lógica.
    /// </summary>
    public static List<TaxBracketDto> GetTaxBracketsForYear(int year, int tenantIdForDto = 0)
    {
        // Panamá: estructura estable desde 2010 (exento hasta $11K, 15%, 25%)
        // Aplicable a 2026, 2027, y años siguientes hasta que cambie la ley.
        return new List<TaxBracketDto>
        {
            new TaxBracketDto(
                Id: 0,
                TenantId: tenantIdForDto,
                Year: year,
                Order: 1,
                Description: "Exento - Hasta $11,000",
                MinIncome: 0.00m,
                MaxIncome: 11000.00m,
                Rate: 0.00m,
                FixedAmount: 0.00m
            ),
            new TaxBracketDto(
                Id: 0,
                TenantId: tenantIdForDto,
                Year: year,
                Order: 2,
                Description: "15% - $11,000 a $50,000",
                MinIncome: 11000.01m,
                MaxIncome: 50000.00m,
                Rate: 15.00m,
                FixedAmount: 0.00m
            ),
            new TaxBracketDto(
                Id: 0,
                TenantId: tenantIdForDto,
                Year: year,
                Order: 3,
                Description: "25% - Más de $50,000",
                MinIncome: 50000.01m,
                MaxIncome: null,
                Rate: 25.00m,
                FixedAmount: 5850.00m
            )
        };
    }

    // ====================================================================
    // Helpers privados
    // ====================================================================

    /// <summary>
    /// Determina los parámetros de la fase vigente según fecha.
    /// </summary>
    private static (DateTime Start, DateTime? End, decimal EmployerRate, string Description)
        DetermineFase(DateTime effectiveDate)
    {
        if (effectiveDate >= Phase3Start)
        {
            return (
                Phase3Start,
                null, // Sin fin
                15.25m,
                "Configuración Panamá — Reforma CSS Fase 3 (15.25% patronal, desde mar 2029)"
            );
        }

        if (effectiveDate >= Phase2Start)
        {
            return (
                Phase2Start,
                Phase2End,
                14.25m,
                "Configuración Panamá — Reforma CSS Fase 2 (14.25% patronal, mar 2027 – feb 2029)"
            );
        }

        // Fase 1 por defecto (incluye fechas anteriores a 2026-01-01 también,
        // porque no tenemos config histórica aquí — es API Platform, no SaaS)
        return (
            Phase1Start,
            Phase1End,
            13.25m,
            "Configuración Panamá 2026 - Reforma CSS (13.25% patronal hasta feb. 2027)"
        );
    }
}
