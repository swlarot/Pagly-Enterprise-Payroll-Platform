// ====================================================================
// Planilla - MockPayrollConfigProvider
// Source: Core360 Stage 5 (Tests)
// Creado: 2025-12-26
// Descripción: Mock de IPayrollConfigProvider para tests unitarios
// Provee configuración de prueba con valores por defecto de CSS, SE, ISR
// ====================================================================

using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;

namespace Vorluno.Planilla.Application.Tests.Helpers;

/// <summary>
/// Mock de IPayrollConfigProvider para tests unitarios.
/// Provee configuración de prueba con valores por defecto según regulaciones de Panamá.
/// </summary>
public class MockPayrollConfigProvider : IPayrollConfigProvider
{
    private readonly PayrollTaxConfigDto? _taxConfig;
    private readonly List<TaxBracketDto> _taxBrackets;
    private readonly bool _returnNullConfig;

    /// <summary>
    /// Constructor que permite inyectar configuración personalizada.
    /// </summary>
    /// <param name="taxConfig">Configuración de tasas (opcional)</param>
    /// <param name="taxBrackets">Brackets de ISR (opcional)</param>
    public MockPayrollConfigProvider(PayrollTaxConfigDto? taxConfig = null, List<TaxBracketDto>? taxBrackets = null)
    {
        _taxConfig = taxConfig ?? CreateDefaultTaxConfig();
        _taxBrackets = taxBrackets ?? CreateDefaultTaxBrackets();
        _returnNullConfig = false;
    }

    /// <summary>
    /// Constructor privado para crear mock que retorna null (simula configuración faltante).
    /// </summary>
    private MockPayrollConfigProvider(bool returnNull)
    {
        _taxConfig = null;
        _taxBrackets = new List<TaxBracketDto>();
        _returnNullConfig = returnNull;
    }

    /// <summary>
    /// Crea un mock que retorna null para simular configuración faltante.
    /// </summary>
    public static MockPayrollConfigProvider WithMissingConfig()
    {
        return new MockPayrollConfigProvider(returnNull: true);
    }

    /// <summary>
    /// Obtiene configuración de tasas de CSS, SE, ISR.
    /// </summary>
    public Task<PayrollTaxConfigDto?> GetTaxConfigAsync(int tenantId, DateTime effectiveDate)
    {
        // Si se configuró para retornar null (simula configuración faltante)
        if (_returnNullConfig)
        {
            return Task.FromResult<PayrollTaxConfigDto?>(null);
        }

        return Task.FromResult(_taxConfig);
    }

    /// <summary>
    /// Obtiene brackets de ISR para un año fiscal.
    /// </summary>
    public Task<List<TaxBracketDto>> GetTaxBracketsAsync(int tenantId, int year)
    {
        return Task.FromResult(_taxBrackets);
    }

    /// <summary>
    /// Crea configuración por defecto según regulaciones de Panamá.
    /// </summary>
    private static PayrollTaxConfigDto CreateDefaultTaxConfig()
    {
        return new PayrollTaxConfigDto(
            Id: 1,
            TenantId: 1,
            EffectiveStartDate: new DateTime(2025, 1, 1),
            EffectiveEndDate: null,

            // CSS - Empleado (fija según Ley 462)
            CssEmployeeRate: 9.75m,

            // CSS - Empleador (escalonada según Ley 462)
            // 2020-2024: 13.25%, 2025-2027: 14.25%, 2028+: 15.25%
            CssEmployerBaseRate: 13.25m,

            // CSS - Topes de cotización según Ley 462
            CssMaxContributionBaseStandard: 1500m,    // Tope estándar: B/. 1,500
            CssMaxContributionBaseIntermediate: 2000m, // Tope intermedio: B/. 2,000 (25+ años, promedio ≥ B/. 2,000)
            CssMaxContributionBaseHigh: 2500m,        // Tope alto: B/. 2,500 (30+ años, promedio ≥ B/. 2,500)

            // CSS - Criterios para topes superiores
            CssIntermediateMinYears: 25,
            CssIntermediateMinAvgSalary: 2000m,
            CssHighMinYears: 30,
            CssHighMinAvgSalary: 2500m,

            // CSS - Riesgo Profesional (tasas patronales)
            CssRiskRateLow: 0.56m,     // Riesgo bajo
            CssRiskRateMedium: 2.50m,  // Riesgo medio
            CssRiskRateHigh: 5.39m,    // Riesgo alto

            // Seguro Educativo (SE)
            EducationalInsuranceEmployeeRate: 1.25m, // 1.25% empleado
            EducationalInsuranceEmployerRate: 1.50m, // 1.50% empleador

            // ISR - Deducción por dependientes
            DependentDeductionAmount: 800m, // B/. 800 por dependiente
            MaxDependents: 3                // Máximo 3 dependientes
        );
    }

    /// <summary>
    /// Crea brackets de ISR por defecto para 2025 según DGI de Panamá.
    /// Tramos:
    /// - B/. 0 - B/. 11,000: 0% (exento)
    /// - B/. 11,001 - B/. 50,000: 15% sobre excedente (fixed = 0)
    /// - B/. 50,001+: 25% sobre excedente (fixed = 5,850)
    /// </summary>
    private static List<TaxBracketDto> CreateDefaultTaxBrackets()
    {
        return new List<TaxBracketDto>
        {
            // Tramo 1: Exento (B/. 0 - B/. 11,000)
            new TaxBracketDto(
                Id: 1,
                TenantId: 1,
                Year: 2025,
                Order: 1,
                Description: "Exento",
                MinIncome: 0m,
                MaxIncome: 11000m,
                Rate: 0m,
                FixedAmount: 0m
            ),

            // Tramo 2: 15% (B/. 11,001 - B/. 50,000)
            new TaxBracketDto(
                Id: 2,
                TenantId: 1,
                Year: 2025,
                Order: 2,
                Description: "15% sobre excedente de B/. 11,000",
                MinIncome: 11000m,
                MaxIncome: 50000m,
                Rate: 15m,
                FixedAmount: 0m
            ),

            // Tramo 3: 25% (B/. 50,001+)
            new TaxBracketDto(
                Id: 3,
                TenantId: 1,
                Year: 2025,
                Order: 3,
                Description: "25% sobre excedente de B/. 50,000",
                MinIncome: 50000m,
                MaxIncome: null, // Sin límite superior (último tramo)
                Rate: 25m,
                FixedAmount: 0m // El servicio calcula el impuesto progresivamente
            )
        };
    }
}
