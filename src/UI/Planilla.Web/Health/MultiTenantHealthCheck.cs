using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Web.Health;

/// <summary>
/// Health check que valida el estado multi-tenant (conteo de tenants activos y conectividad a datos).
/// </summary>
public class MultiTenantHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public MultiTenantHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var activeTenants = await db.Tenants.CountAsync(t => t.IsActive, cancellationToken);
            var totalTenants = await db.Tenants.CountAsync(cancellationToken);

            var data = new Dictionary<string, object>
            {
                ["activeTenants"] = activeTenants,
                ["totalTenants"] = totalTenants
            };

            return activeTenants >= 0
                ? HealthCheckResult.Healthy("Multi-tenant data available", data)
                : HealthCheckResult.Unhealthy("No tenant data", data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Multi-tenant check failed", ex);
        }
    }
}
