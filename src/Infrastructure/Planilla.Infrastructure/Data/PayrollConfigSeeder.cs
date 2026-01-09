// ====================================================================
// Planilla - PayrollConfigSeeder
// Source: Phase A - Configuration Seeding
// Creado: 2025-12-26
// Descripción: Seeder para cargar configuración inicial de planilla
// Lee de docs/seeds/*.json e inserta en DB de forma idempotente
// ====================================================================

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vorluno.Planilla.Domain.Entities;

namespace Vorluno.Planilla.Infrastructure.Data;

/// <summary>
/// Seeder para cargar configuración inicial de planilla desde archivos JSON.
/// Inserta datos de forma idempotente (no duplica si ya existen).
/// </summary>
public static class PayrollConfigSeeder
{
    /// <summary>
    /// Ejecuta el seed de configuración de planilla y tax brackets.
    /// </summary>
    /// <param name="context">ApplicationDbContext</param>
    /// <param name="logger">Logger opcional para logs de seeding</param>
    public static async Task SeedAsync(ApplicationDbContext context, ILogger? logger = null)
    {
        logger?.LogInformation("Iniciando seed de configuración de planilla...");

        try
        {
            // Seed PayrollTaxConfiguration
            await SeedPayrollConfigAsync(context, logger);

            // Seed TaxBrackets
            await SeedTaxBracketsAsync(context, logger);

            logger?.LogInformation("Seed de configuración de planilla completado exitosamente");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error al ejecutar seed de configuración de planilla");
            throw;
        }
    }

    private static async Task SeedPayrollConfigAsync(ApplicationDbContext context, ILogger? logger)
    {
        // Verificar si ya hay configuración (ignorar query filters durante seeding)
        var existingCount = await context.PayrollTaxConfigurations.IgnoreQueryFilters().CountAsync();
        if (existingCount > 0)
        {
            logger?.LogInformation("PayrollTaxConfiguration ya tiene {Count} registros. Saltando seed.", existingCount);
            return;
        }

        // Buscar archivo JSON en múltiples ubicaciones posibles
        var possiblePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "docs", "seeds", "seed_payroll_config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "docs", "seeds", "seed_payroll_config.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "seeds", "seed_payroll_config.json"),
            "C:\\Planilla\\docs\\seeds\\seed_payroll_config.json" // Absolute path as fallback
        };

        string? normalizedPath = null;
        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                normalizedPath = fullPath;
                break;
            }
        }

        if (normalizedPath == null)
        {
            logger?.LogWarning("Archivo seed_payroll_config.json no encontrado en ninguna ubicación. Ubicaciones probadas: {Paths}", string.Join(", ", possiblePaths.Select(Path.GetFullPath)));
            return;
        }

        logger?.LogInformation("Leyendo seed_payroll_config.json desde {Path}", normalizedPath);

        var jsonContent = await File.ReadAllTextAsync(normalizedPath);
        var seedData = JsonSerializer.Deserialize<PayrollConfigSeedData>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (seedData?.Configurations == null || seedData.Configurations.Count == 0)
        {
            logger?.LogWarning("No se encontraron configuraciones en seed_payroll_config.json");
            return;
        }

        // Insertar configuraciones
        foreach (var config in seedData.Configurations)
        {
            var entity = new PayrollTaxConfiguration
            {
                TenantId = config.TenantId,
                EffectiveStartDate = config.EffectiveStartDate,
                EffectiveEndDate = config.EffectiveEndDate,
                Description = config.Description,
                CssEmployeeRate = config.CssEmployeeRate,
                CssEmployerBaseRate = config.CssEmployerBaseRate,
                CssRiskRateLow = config.CssRiskRateLow,
                CssRiskRateMedium = config.CssRiskRateMedium,
                CssRiskRateHigh = config.CssRiskRateHigh,
                CssMaxContributionBaseStandard = config.CssMaxContributionBaseStandard,
                CssMaxContributionBaseIntermediate = config.CssMaxContributionBaseIntermediate,
                CssMaxContributionBaseHigh = config.CssMaxContributionBaseHigh,
                CssIntermediateMinYears = config.CssIntermediateMinYears,
                CssIntermediateMinAvgSalary = config.CssIntermediateMinAvgSalary,
                CssHighMinYears = config.CssHighMinYears,
                CssHighMinAvgSalary = config.CssHighMinAvgSalary,
                EducationalInsuranceEmployeeRate = config.EducationalInsuranceEmployeeRate,
                EducationalInsuranceEmployerRate = config.EducationalInsuranceEmployerRate,
                DependentDeductionAmount = config.DependentDeductionAmount,
                MaxDependents = config.MaxDependents,
                IsActive = config.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.PayrollTaxConfigurations.Add(entity);
        }

        await context.SaveChangesAsync();
        logger?.LogInformation("Insertadas {Count} configuraciones de planilla", seedData.Configurations.Count);
    }

    private static async Task SeedTaxBracketsAsync(ApplicationDbContext context, ILogger? logger)
    {
        // Verificar si ya hay brackets (ignorar query filters durante seeding)
        var existingCount = await context.TaxBrackets.IgnoreQueryFilters().CountAsync();
        if (existingCount > 0)
        {
            logger?.LogInformation("TaxBrackets ya tiene {Count} registros. Saltando seed.", existingCount);
            return;
        }

        // Buscar archivo JSON en múltiples ubicaciones posibles
        var possiblePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "docs", "seeds", "seed_tax_brackets_2025.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "docs", "seeds", "seed_tax_brackets_2025.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "seeds", "seed_tax_brackets_2025.json"),
            "C:\\Planilla\\docs\\seeds\\seed_tax_brackets_2025.json" // Absolute path as fallback
        };

        string? normalizedPath = null;
        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                normalizedPath = fullPath;
                break;
            }
        }

        if (normalizedPath == null)
        {
            logger?.LogWarning("Archivo seed_tax_brackets_2025.json no encontrado en ninguna ubicación. Ubicaciones probadas: {Paths}", string.Join(", ", possiblePaths.Select(Path.GetFullPath)));
            return;
        }

        logger?.LogInformation("Leyendo seed_tax_brackets_2025.json desde {Path}", normalizedPath);

        var jsonContent = await File.ReadAllTextAsync(normalizedPath);
        var seedData = JsonSerializer.Deserialize<TaxBracketsSeedData>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (seedData?.Brackets == null || seedData.Brackets.Count == 0)
        {
            logger?.LogWarning("No se encontraron brackets en seed_tax_brackets_2025.json");
            return;
        }

        // Insertar brackets
        foreach (var bracket in seedData.Brackets)
        {
            var entity = new TaxBracket
            {
                TenantId = bracket.TenantId,
                Year = bracket.Year,
                Order = bracket.Order,
                Description = bracket.Description,
                MinIncome = bracket.MinIncome,
                MaxIncome = bracket.MaxIncome,
                Rate = bracket.Rate,
                FixedAmount = bracket.FixedAmount,
                IsActive = bracket.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.TaxBrackets.Add(entity);
        }

        await context.SaveChangesAsync();
        logger?.LogInformation("Insertados {Count} tax brackets", seedData.Brackets.Count);
    }

    // DTOs para deserialización de JSON
    private class PayrollConfigSeedData
    {
        public List<PayrollConfigSeedItem> Configurations { get; set; } = new();
    }

    private class PayrollConfigSeedItem
    {
        public int TenantId { get; set; }
        public DateTime EffectiveStartDate { get; set; }
        public DateTime? EffectiveEndDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal CssEmployeeRate { get; set; }
        public decimal CssEmployerBaseRate { get; set; }
        public decimal CssRiskRateLow { get; set; }
        public decimal CssRiskRateMedium { get; set; }
        public decimal CssRiskRateHigh { get; set; }
        public decimal CssMaxContributionBaseStandard { get; set; }
        public decimal CssMaxContributionBaseIntermediate { get; set; }
        public decimal CssMaxContributionBaseHigh { get; set; }
        public int CssIntermediateMinYears { get; set; }
        public decimal CssIntermediateMinAvgSalary { get; set; }
        public int CssHighMinYears { get; set; }
        public decimal CssHighMinAvgSalary { get; set; }
        public decimal EducationalInsuranceEmployeeRate { get; set; }
        public decimal EducationalInsuranceEmployerRate { get; set; }
        public decimal DependentDeductionAmount { get; set; }
        public int MaxDependents { get; set; }
        public bool IsActive { get; set; }
    }

    private class TaxBracketsSeedData
    {
        public List<TaxBracketSeedItem> Brackets { get; set; } = new();
    }

    private class TaxBracketSeedItem
    {
        public int TenantId { get; set; }
        public int Year { get; set; }
        public int Order { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal MinIncome { get; set; }
        public decimal? MaxIncome { get; set; }
        public decimal Rate { get; set; }
        public decimal FixedAmount { get; set; }
        public bool IsActive { get; set; }
    }
}
