// ====================================================================
// Planilla - PayrollConfigSeeder (Multi-Tenant Fixed)
// Source: Phase 3 - Multi-Tenant Seeding
// Modificado: 2026-01-09
// Descripción: Seeder multi-tenant que NO depende de JWT/TenantContext
// Crea config para cada tenant activo de forma idempotente
// ====================================================================

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vorluno.Planilla.Domain.Entities;

namespace Vorluno.Planilla.Infrastructure.Data;

/// <summary>
/// Seeder multi-tenant para configuración de planilla.
/// Crea config para CADA tenant activo sin depender de JWT/TenantContext.
/// </summary>
public static class PayrollConfigSeeder
{
    /// <summary>
    /// Ejecuta el seed de configuración para todos los tenants activos.
    /// </summary>
    public static async Task SeedAsync(ApplicationDbContext context, ILogger? logger = null)
    {
        logger?.LogInformation("Iniciando seed de configuración multi-tenant...");

        try
        {
            // Obtener todos los tenants activos (SIN filtros globales)
            var tenants = await context.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.IsActive)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();

            if (!tenants.Any())
            {
                logger?.LogWarning("No hay tenants activos. Saltando seed de configuración.");
                return;
            }

            logger?.LogInformation("Encontrados {Count} tenants activos para seeding", tenants.Count);

            // Seed por cada tenant
            foreach (var tenant in tenants)
            {
                logger?.LogInformation("Procesando seed para Tenant {TenantId} ({TenantName})...",
                    tenant.Id, tenant.Name);

                await SeedPayrollConfigForTenantAsync(context, tenant.Id, logger);
                await SeedPayrollConfigPhase2ForTenantAsync(context, tenant.Id, logger);
                await SeedPayrollConfigPhase3ForTenantAsync(context, tenant.Id, logger);
                await SeedTaxBracketsForTenantAsync(context, tenant.Id, logger);
            }

            logger?.LogInformation("Seed multi-tenant completado exitosamente");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error al ejecutar seed de configuración");
            // NO relanzar en dev para permitir que la app arranque
            if (logger != null)
            {
                logger.LogWarning("Continuando startup a pesar del error de seeding");
            }
        }
    }

    /// <summary>
    /// Seed de PayrollTaxConfiguration para un tenant específico.
    /// </summary>
    private static async Task SeedPayrollConfigForTenantAsync(
        ApplicationDbContext context,
        int tenantId,
        ILogger? logger)
    {
        // Check si ya existe config para este tenant (IGNORAR filtros globales)
        var existingConfig = await context.PayrollTaxConfigurations
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .FirstOrDefaultAsync();

        if (existingConfig != null)
        {
            // Actualizar tasas si quedaron desactualizadas (ej: Reforma CSS 13.25%)
            bool updated = false;
            if (existingConfig.CssEmployerBaseRate < 13.25m)
            {
                logger?.LogInformation("Tenant {TenantId}: actualizando CssEmployerBaseRate {Old}% → 13.25% (Reforma CSS)", tenantId, existingConfig.CssEmployerBaseRate);
                existingConfig.CssEmployerBaseRate = 13.25m;
                updated = true;
            }
            // Actualizar tasas de riesgo profesional a valores correctos (Acuerdo N°2 de 1995)
            if (existingConfig.CssRiskRateLow != 0.56m || existingConfig.CssRiskRateMedium != 2.10m || existingConfig.CssRiskRateHigh != 5.67m)
            {
                existingConfig.CssRiskRateLow = 0.56m;
                existingConfig.CssRiskRateMedium = 2.10m;
                existingConfig.CssRiskRateHigh = 5.67m;
                updated = true;
                logger?.LogInformation("Tenant {TenantId}: actualizando tasas riesgo → 0.56%/2.10%/5.67% (Acuerdo N°2 de 1995)", tenantId);
            }
            // Corregir MaxDependents: Ley 37/2018 + Decreto 368/2018 no establece límite
            if (existingConfig.MaxDependents < 99)
            {
                existingConfig.MaxDependents = 99;
                updated = true;
                logger?.LogInformation("Tenant {TenantId}: actualizando MaxDependents → 99 (sin límite legal)", tenantId);
            }
            if (updated)
            {
                existingConfig.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
                logger?.LogInformation("✓ Configuración actualizada para Tenant {TenantId}", tenantId);
            }
            else
            {
                logger?.LogInformation("Tenant {TenantId} ya tiene configuración actualizada. Saltando.", tenantId);
            }
            return;
        }

        // Crear configuración default para Panamá 2026
        // Reforma CSS - Tasas patronales escalonadas:
        //   Hasta feb. 2027: 13.25%
        //   Mar. 2027 – feb. 2029: 14.25% (crear nueva config con EffectiveStartDate = 2027-03-01)
        //   Desde mar. 2029: 15.25% (crear nueva config con EffectiveStartDate = 2029-03-01)
        var config = new PayrollTaxConfiguration
        {
            TenantId = tenantId,  // CRÍTICO: asignar tenant
            EffectiveStartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveEndDate = new DateTime(2027, 2, 28, 23, 59, 59, DateTimeKind.Utc), // Vigente hasta feb. 2027
            Description = "Configuración Panamá 2026 - Reforma CSS (13.25% patronal hasta feb. 2027)",

            // Tasas CSS (Caja de Seguro Social)
            CssEmployeeRate = 9.75m,      // Empleado: 9.75%
            CssEmployerBaseRate = 13.25m,  // Empleador base: 13.25% (vigente hasta feb. 2027, reforma CSS)

            // Tasas de riesgo profesional (Acuerdo N°2 de 1995 — 5 clases)
            // Nota: el empleado almacena su clase directamente; estos campos son referencia
            CssRiskRateLow = 0.56m,        // Clase I — Riesgo Mínimo (oficinas, administración)
            CssRiskRateMedium = 2.10m,     // Clase III — Riesgo Medio (transporte, manufactura)
            CssRiskRateHigh = 5.67m,       // Clase V — Riesgo Máximo (construcción, minería)

            // Tope de pensión CSS (Art. 178 Ley 462) — NO es tope de cotización
            // La cotización CSS se calcula sobre el salario bruto completo
            CssMaxContributionBaseStandard = 1500.00m,      // Tope pensión: $1,500
            CssMaxContributionBaseIntermediate = 2000.00m,  // Tope pensión intermedio: $2,000
            CssMaxContributionBaseHigh = 2500.00m,          // Tope pensión alto: $2,500

            // Requisitos para topes de pensión superiores (años mínimos cotizados)
            CssIntermediateMinYears = 25,
            CssIntermediateMinAvgSalary = 2000.00m,
            CssHighMinYears = 30,
            CssHighMinAvgSalary = 2500.00m,

            // Seguro Educativo
            EducationalInsuranceEmployeeRate = 1.25m,   // Empleado: 1.25%
            EducationalInsuranceEmployerRate = 1.50m,   // Empleador: 1.50%

            // ISR (Impuesto Sobre la Renta)
            DependentDeductionAmount = 800.00m,  // $800 por dependiente
            MaxDependents = 99,  // Sin límite legal (Ley 37/2018 + Decreto 368/2018)

            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.PayrollTaxConfigurations.Add(config);
        await context.SaveChangesAsync();

        logger?.LogInformation("✓ Configuración creada para Tenant {TenantId}", tenantId);
    }

    /// <summary>
    /// Seed de PayrollTaxConfiguration Fase 2 (mar 2027 – feb 2029): cuota patronal 14.25%.
    /// Reforma CSS Ley 462 — segunda fase escalonada.
    /// </summary>
    private static async Task SeedPayrollConfigPhase2ForTenantAsync(
        ApplicationDbContext context,
        int tenantId,
        ILogger? logger)
    {
        var phase2Start = new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var exists = await context.PayrollTaxConfigurations
            .IgnoreQueryFilters()
            .AnyAsync(c => c.TenantId == tenantId && c.EffectiveStartDate == phase2Start);

        if (exists)
        {
            logger?.LogInformation("Tenant {TenantId} ya tiene configuración Fase 2 (2027). Saltando.", tenantId);
            return;
        }

        var config = new PayrollTaxConfiguration
        {
            TenantId = tenantId,
            EffectiveStartDate = phase2Start,
            EffectiveEndDate = new DateTime(2029, 2, 28, 23, 59, 59, DateTimeKind.Utc),
            Description = "Configuración Panamá — Reforma CSS Fase 2 (14.25% patronal, mar 2027 – feb 2029)",
            CssEmployeeRate = 9.75m,
            CssEmployerBaseRate = 14.25m,
            CssRiskRateLow = 0.56m,
            CssRiskRateMedium = 2.10m,
            CssRiskRateHigh = 5.67m,
            CssMaxContributionBaseStandard = 1500.00m,
            CssMaxContributionBaseIntermediate = 2000.00m,
            CssMaxContributionBaseHigh = 2500.00m,
            CssIntermediateMinYears = 25,
            CssIntermediateMinAvgSalary = 2000.00m,
            CssHighMinYears = 30,
            CssHighMinAvgSalary = 2500.00m,
            EducationalInsuranceEmployeeRate = 1.25m,
            EducationalInsuranceEmployerRate = 1.50m,
            DependentDeductionAmount = 800.00m,
            MaxDependents = 99,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.PayrollTaxConfigurations.Add(config);
        await context.SaveChangesAsync();

        logger?.LogInformation("✓ Configuración Fase 2 (2027) creada para Tenant {TenantId}", tenantId);
    }

    /// <summary>
    /// Seed de PayrollTaxConfiguration Fase 3 (desde mar 2029): cuota patronal 15.25%.
    /// Reforma CSS Ley 462 — tercera fase escalonada (vigente indefinidamente).
    /// </summary>
    private static async Task SeedPayrollConfigPhase3ForTenantAsync(
        ApplicationDbContext context,
        int tenantId,
        ILogger? logger)
    {
        var phase3Start = new DateTime(2029, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var exists = await context.PayrollTaxConfigurations
            .IgnoreQueryFilters()
            .AnyAsync(c => c.TenantId == tenantId && c.EffectiveStartDate == phase3Start);

        if (exists)
        {
            logger?.LogInformation("Tenant {TenantId} ya tiene configuración Fase 3 (2029). Saltando.", tenantId);
            return;
        }

        var config = new PayrollTaxConfiguration
        {
            TenantId = tenantId,
            EffectiveStartDate = phase3Start,
            EffectiveEndDate = null, // Vigente indefinidamente
            Description = "Configuración Panamá — Reforma CSS Fase 3 (15.25% patronal, desde mar 2029)",
            CssEmployeeRate = 9.75m,
            CssEmployerBaseRate = 15.25m,
            CssRiskRateLow = 0.56m,
            CssRiskRateMedium = 2.10m,
            CssRiskRateHigh = 5.67m,
            CssMaxContributionBaseStandard = 1500.00m,
            CssMaxContributionBaseIntermediate = 2000.00m,
            CssMaxContributionBaseHigh = 2500.00m,
            CssIntermediateMinYears = 25,
            CssIntermediateMinAvgSalary = 2000.00m,
            CssHighMinYears = 30,
            CssHighMinAvgSalary = 2500.00m,
            EducationalInsuranceEmployeeRate = 1.25m,
            EducationalInsuranceEmployerRate = 1.50m,
            DependentDeductionAmount = 800.00m,
            MaxDependents = 99,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.PayrollTaxConfigurations.Add(config);
        await context.SaveChangesAsync();

        logger?.LogInformation("✓ Configuración Fase 3 (2029) creada para Tenant {TenantId}", tenantId);
    }

    /// <summary>
    /// Seed de TaxBrackets (ISR) para un tenant específico.
    /// DEV-29: Incluye 2026 y 2027 para evitar excepción al calcular ISR en 2027.
    /// </summary>
    private static async Task SeedTaxBracketsForTenantAsync(
        ApplicationDbContext context,
        int tenantId,
        ILogger? logger)
    {
        await SeedTaxBracketsForYearAsync(context, tenantId, 2026, logger);
        await SeedTaxBracketsForYearAsync(context, tenantId, 2027, logger);
    }

    /// <summary>
    /// Seed de TaxBrackets (ISR) para un tenant y año fiscal.
    /// Estructura Panamá: exento hasta $11,000, 15% hasta $50,000, 25% resto.
    /// </summary>
    private static async Task SeedTaxBracketsForYearAsync(
        ApplicationDbContext context,
        int tenantId,
        int year,
        ILogger? logger)
    {
        var existingCount = await context.TaxBrackets
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && b.Year == year)
            .CountAsync();

        if (existingCount > 0)
        {
            logger?.LogInformation("Tenant {TenantId} ya tiene tax brackets para {Year}. Saltando.", tenantId, year);
            return;
        }

        var brackets = new List<TaxBracket>
        {
            new TaxBracket
            {
                TenantId = tenantId,
                Year = year,
                Order = 1,
                Description = "Exento - Hasta $11,000",
                MinIncome = 0.00m,
                MaxIncome = 11000.00m,
                Rate = 0.00m,
                FixedAmount = 0.00m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new TaxBracket
            {
                TenantId = tenantId,
                Year = year,
                Order = 2,
                Description = "15% - $11,000 a $50,000",
                MinIncome = 11000.01m,
                MaxIncome = 50000.00m,
                Rate = 15.00m,
                FixedAmount = 0.00m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new TaxBracket
            {
                TenantId = tenantId,
                Year = year,
                Order = 3,
                Description = "25% - Más de $50,000",
                MinIncome = 50000.01m,
                MaxIncome = null,
                Rate = 25.00m,
                FixedAmount = 5850.00m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        context.TaxBrackets.AddRange(brackets);
        await context.SaveChangesAsync();

        logger?.LogInformation("✓ {Count} tax brackets creados para Tenant {TenantId} año {Year}",
            brackets.Count, tenantId, year);
    }

    /// <summary>
    /// Seed de configuración para un tenant recién creado.
    /// Llamar desde TenantService al crear nuevo tenant.
    /// </summary>
    public static async Task SeedForNewTenantAsync(
        ApplicationDbContext context,
        int tenantId,
        ILogger? logger = null)
    {
        logger?.LogInformation("Seeding configuración para nuevo Tenant {TenantId}...", tenantId);

        await SeedPayrollConfigForTenantAsync(context, tenantId, logger);
        await SeedPayrollConfigPhase2ForTenantAsync(context, tenantId, logger);
        await SeedPayrollConfigPhase3ForTenantAsync(context, tenantId, logger);
        await SeedTaxBracketsForTenantAsync(context, tenantId, logger);

        logger?.LogInformation("✓ Seed completado para nuevo Tenant {TenantId}", tenantId);
    }
}
