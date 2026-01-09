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
            logger?.LogInformation("Tenant {TenantId} ya tiene configuración. Saltando.", tenantId);
            return;
        }

        // Crear configuración default para Panamá 2026
        var config = new PayrollTaxConfiguration
        {
            TenantId = tenantId,  // CRÍTICO: asignar tenant
            EffectiveStartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveEndDate = null,
            Description = "Configuración Panamá 2026 - Ley 462 CSS",

            // Tasas CSS (Caja de Seguro Social)
            CssEmployeeRate = 9.75m,      // Empleado: 9.75%
            CssEmployerBaseRate = 12.25m,  // Empleador base: 12.25%

            // Tasas de riesgo profesional
            CssRiskRateLow = 0.41m,        // Riesgo bajo: 0.41%
            CssRiskRateMedium = 1.09m,     // Riesgo medio: 1.09%
            CssRiskRateHigh = 2.31m,       // Riesgo alto: 2.31%

            // Topes de cotización CSS (Ley 462)
            CssMaxContributionBaseStandard = 1000.00m,      // Estándar: $1,000
            CssMaxContributionBaseIntermediate = 1500.00m,  // Intermedio: $1,500
            CssMaxContributionBaseHigh = 2500.00m,          // Alto: $2,500

            // Requisitos para topes superiores
            CssIntermediateMinYears = 5,
            CssIntermediateMinAvgSalary = 850.00m,
            CssHighMinYears = 10,
            CssHighMinAvgSalary = 1200.00m,

            // Seguro Educativo
            EducationalInsuranceEmployeeRate = 1.25m,   // Empleado: 1.25%
            EducationalInsuranceEmployerRate = 1.50m,   // Empleador: 1.50%

            // ISR (Impuesto Sobre la Renta)
            DependentDeductionAmount = 800.00m,  // $800 por dependiente
            MaxDependents = 5,

            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.PayrollTaxConfigurations.Add(config);
        await context.SaveChangesAsync();

        logger?.LogInformation("✓ Configuración creada para Tenant {TenantId}", tenantId);
    }

    /// <summary>
    /// Seed de TaxBrackets (ISR) para un tenant específico.
    /// </summary>
    private static async Task SeedTaxBracketsForTenantAsync(
        ApplicationDbContext context,
        int tenantId,
        ILogger? logger)
    {
        // Check si ya existen brackets para este tenant
        var existingCount = await context.TaxBrackets
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && b.Year == 2026)
            .CountAsync();

        if (existingCount > 0)
        {
            logger?.LogInformation("Tenant {TenantId} ya tiene {Count} tax brackets para 2026. Saltando.",
                tenantId, existingCount);
            return;
        }

        // Tax brackets ISR Panamá 2026 (valores anuales)
        var brackets = new List<TaxBracket>
        {
            new TaxBracket
            {
                TenantId = tenantId,
                Year = 2026,
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
                Year = 2026,
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
                Year = 2026,
                Order = 3,
                Description = "25% - Más de $50,000",
                MinIncome = 50000.01m,
                MaxIncome = null,  // Sin límite superior
                Rate = 25.00m,
                FixedAmount = 5850.00m,  // 15% de $39,000 = $5,850
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        context.TaxBrackets.AddRange(brackets);
        await context.SaveChangesAsync();

        logger?.LogInformation("✓ {Count} tax brackets creados para Tenant {TenantId}",
            brackets.Count, tenantId);
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
        await SeedTaxBracketsForTenantAsync(context, tenantId, logger);

        logger?.LogInformation("✓ Seed completado para nuevo Tenant {TenantId}", tenantId);
    }
}
