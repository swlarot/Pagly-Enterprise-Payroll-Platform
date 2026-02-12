using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Models;
using Microsoft.Extensions.Configuration;
using Vorluno.Planilla.Infrastructure.Data;

namespace Planilla.Web.IntegrationTests;

/// <summary>
/// Helper para crear tenants directamente en la DB de tests,
/// evitando el endpoint /api/auth/register (deshabilitado en producción).
/// </summary>
public static class TestTenantHelper
{
    public static async Task<string> CreateTenantAndGetTokenAsync(
        CustomWebApplicationFactory factory, string companyName)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var email = $"test-{Guid.NewGuid()}@example.com";

        // 1. Crear usuario
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            NombreCompleto = companyName
        };
        var result = await userManager.CreateAsync(user, "Test@1234");
        if (!result.Succeeded)
            throw new Exception($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        // 2. Crear tenant
        var tenant = new Tenant
        {
            Name = companyName,
            Subdomain = $"test-{Guid.NewGuid():N}".Substring(0, 20),
            RUC = Guid.NewGuid().ToString().Substring(0, 8),
            DV = "12",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        // 3. Crear subscription
        var limits = PlanFeatures.GetLimits(SubscriptionPlan.Professional);
        var subscription = new Subscription
        {
            TenantId = tenant.Id,
            Plan = SubscriptionPlan.Professional,
            Status = SubscriptionStatus.Trialing,
            StartDate = DateTime.UtcNow,
            TrialEndsAt = DateTime.UtcNow.AddDays(14),
            MonthlyPrice = limits.PricePerMonth,
            CreatedAt = DateTime.UtcNow
        };
        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync();

        tenant.SubscriptionId = subscription.Id;
        context.Tenants.Update(tenant);
        await context.SaveChangesAsync();

        // 4. Crear TenantUser
        var tenantUser = new TenantUser
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = TenantRole.Owner,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        context.TenantUsers.Add(tenantUser);
        await context.SaveChangesAsync();

        // 5. Crear PayrollTaxConfiguration default (requerida para cálculos de planilla)
        var taxConfig = new PayrollTaxConfiguration
        {
            TenantId = tenant.Id,
            EffectiveStartDate = new DateTime(2020, 1, 1),
            EffectiveEndDate = null,
            Description = "Test Config",
            IsActive = true,
            CssEmployeeRate = 9.75m,
            CssEmployerBaseRate = 12.25m,
            CssRiskRateLow = 0.56m,
            CssRiskRateMedium = 2.10m,
            CssRiskRateHigh = 5.67m,
            CssMaxContributionBaseStandard = 1500m,
            CssMaxContributionBaseIntermediate = 2000m,
            CssMaxContributionBaseHigh = 2500m,
            CssIntermediateMinYears = 25,
            CssIntermediateMinAvgSalary = 2000m,
            CssHighMinYears = 30,
            CssHighMinAvgSalary = 2500m,
            EducationalInsuranceEmployeeRate = 1.25m,
            EducationalInsuranceEmployerRate = 1.50m,
            DependentDeductionAmount = 800m,
            MaxDependents = 3,
            CreatedAt = DateTime.UtcNow
        };
        context.Set<PayrollTaxConfiguration>().Add(taxConfig);
        await context.SaveChangesAsync();

        // 6. Crear TaxBrackets ISR (Panamá 2026)
        var brackets = new[]
        {
            new TaxBracket { TenantId = tenant.Id, Year = 2026, Order = 1, Description = "Exento", MinIncome = 0, MaxIncome = 11000, Rate = 0, FixedAmount = 0, IsActive = true },
            new TaxBracket { TenantId = tenant.Id, Year = 2026, Order = 2, Description = "15%", MinIncome = 11000.01m, MaxIncome = 50000, Rate = 15, FixedAmount = 0, IsActive = true },
            new TaxBracket { TenantId = tenant.Id, Year = 2026, Order = 3, Description = "25%", MinIncome = 50000.01m, MaxIncome = null, Rate = 25, FixedAmount = 5850, IsActive = true },
        };
        context.Set<TaxBracket>().AddRange(brackets);
        await context.SaveChangesAsync();

        // 7. Generar JWT
        var jwtKey = config["Jwt:Key"] ?? "test-secret-key-must-be-at-least-32-characters-long-for-hmacsha256";
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("tenant_id", tenant.Id.ToString()),
            new Claim("tenant_role", "Owner"),
            new Claim(ClaimTypes.Role, "Owner"),
            new Claim("plan", "Professional"),
            new Claim("is_system_admin", "false")
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"] ?? "https://test.planilla.vorluno.dev",
            audience: config["Jwt:Audience"] ?? "https://test.planilla.vorluno.dev",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
