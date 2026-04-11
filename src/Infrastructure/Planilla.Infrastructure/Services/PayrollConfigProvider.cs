// ====================================================================
// Planilla - PayrollConfigProvider
// Source: Core360 Stage 2
// Portado: 2025-12-26
// Descripción: Proveedor de configuración de planilla desde base de datos
//
// DEV — Refactor API Platform: eliminado fallback silencioso a ITenantContext.
// Ahora si companyId <= 0 se lanza ArgumentException explícita. Esto cierra un
// vector de tenant leakage: antes, un endpoint stateless que llamara con
// companyId=0 leía config del tenant del HttpContext actual (otro cliente).
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Implementación de IPayrollConfigProvider que obtiene configuración desde ApplicationDbContext.
/// Utiliza AsNoTracking() para queries de solo lectura (mejor performance).
/// Requiere companyId explícito — ya NO hace fallback a ITenantContext.
/// </summary>
public class PayrollConfigProvider : IPayrollConfigProvider
{
    private readonly ApplicationDbContext _context;

    public PayrollConfigProvider(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene la configuración de tasas vigente para una fecha específica.
    /// Busca config donde effectiveDate esté entre EffectiveStartDate y EffectiveEndDate (o End sea null).
    /// </summary>
    /// <param name="companyId">ID del tenant (requerido, &gt; 0)</param>
    /// <param name="effectiveDate">Fecha para determinar configuración vigente</param>
    /// <returns>Configuración vigente o null si no existe</returns>
    /// <exception cref="ArgumentException">Si companyId &lt;= 0</exception>
    public async Task<PayrollTaxConfigDto?> GetTaxConfigAsync(int companyId, DateTime effectiveDate)
    {
        if (companyId <= 0)
        {
            throw new ArgumentException(
                "companyId debe ser mayor a 0. PayrollConfigProvider requiere un tenant explícito. " +
                "Si necesita configuración stateless use StaticPayrollConfigProvider.",
                nameof(companyId));
        }

        var tenantId = companyId;

        var config = await _context.PayrollTaxConfigurations
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId
                     && c.IsActive
                     && c.EffectiveStartDate <= effectiveDate
                     && (c.EffectiveEndDate == null || c.EffectiveEndDate >= effectiveDate))
            .Select(c => new PayrollTaxConfigDto(
                c.Id,
                c.TenantId,
                c.EffectiveStartDate,
                c.EffectiveEndDate,
                c.CssEmployeeRate,
                c.CssEmployerBaseRate,
                c.CssRiskRateLow,
                c.CssRiskRateMedium,
                c.CssRiskRateHigh,
                c.CssMaxContributionBaseStandard,
                c.CssMaxContributionBaseIntermediate,
                c.CssMaxContributionBaseHigh,
                c.CssIntermediateMinYears,
                c.CssIntermediateMinAvgSalary,
                c.CssHighMinYears,
                c.CssHighMinAvgSalary,
                c.EducationalInsuranceEmployeeRate,
                c.EducationalInsuranceEmployerRate,
                c.DependentDeductionAmount,
                c.MaxDependents,
                c.SalarioMinimoLegal  // DEV-28: incluir en DTO
            ))
            .FirstOrDefaultAsync();

        return config;
    }

    /// <summary>
    /// Obtiene los brackets de ISR para un año fiscal específico.
    /// Retorna brackets ordenados por Order ASC para cálculo secuencial.
    /// </summary>
    /// <param name="companyId">ID del tenant (requerido, &gt; 0)</param>
    /// <param name="year">Año fiscal (ej: 2025)</param>
    /// <returns>Lista de brackets ordenados. Lista vacía si no existen brackets.</returns>
    /// <exception cref="ArgumentException">Si companyId &lt;= 0</exception>
    public async Task<List<TaxBracketDto>> GetTaxBracketsAsync(int companyId, int year)
    {
        if (companyId <= 0)
        {
            throw new ArgumentException(
                "companyId debe ser mayor a 0. PayrollConfigProvider requiere un tenant explícito. " +
                "Si necesita configuración stateless use StaticPayrollConfigProvider.",
                nameof(companyId));
        }

        var tenantId = companyId;

        var brackets = await _context.TaxBrackets
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                     && b.Year == year
                     && b.IsActive)
            .OrderBy(b => b.Order)
            .Select(b => new TaxBracketDto(
                b.Id,
                b.TenantId,
                b.Year,
                b.Order,
                b.Description,
                b.MinIncome,
                b.MaxIncome,
                b.Rate,
                b.FixedAmount
            ))
            .ToListAsync();

        // DEV-29: Fallback al año más reciente disponible si no hay brackets para el año solicitado.
        // Evita PayrollConfigurationException en 2027+ si el admin no ha configurado nuevos brackets.
        if (brackets.Count == 0)
        {
            var mostRecentYear = await _context.TaxBrackets
                .AsNoTracking()
                .Where(b => b.TenantId == tenantId && b.IsActive && b.Year < year)
                .MaxAsync(b => (int?)b.Year);

            if (mostRecentYear.HasValue)
            {
                brackets = await _context.TaxBrackets
                    .AsNoTracking()
                    .Where(b => b.TenantId == tenantId && b.Year == mostRecentYear.Value && b.IsActive)
                    .OrderBy(b => b.Order)
                    .Select(b => new TaxBracketDto(
                        b.Id, b.TenantId, b.Year, b.Order, b.Description,
                        b.MinIncome, b.MaxIncome, b.Rate, b.FixedAmount
                    ))
                    .ToListAsync();
            }
        }

        return brackets;
    }
}
