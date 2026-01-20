using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Models;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Plan Limit Service - Enforces subscription limits and feature flags
/// CRITICAL: This service is the gatekeeper for SaaS features
/// </summary>
public class PlanLimitService : IPlanLimitService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PlanLimitService> _logger;

    public PlanLimitService(
        ApplicationDbContext context,
        ILogger<PlanLimitService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets plan limits for a tenant based on their subscription
    /// </summary>
    public async Task<PlanLimits> GetLimitsForTenantAsync(int tenantId)
    {
        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);

        if (subscription == null)
        {
            _logger.LogWarning("Tenant {TenantId} no tiene suscripción, usando Free", tenantId);
            return PlanFeatures.GetLimits(SubscriptionPlan.Free);
        }

        return PlanFeatures.GetLimits(subscription.Plan);
    }

    /// <summary>
    /// Checks if tenant can create a new employee
    /// </summary>
    public async Task<(bool allowed, string? reason)> CanCreateEmployeeAsync(int tenantId)
    {
        try
        {
            // 1. Get subscription
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            // If no subscription exists, check if tenant exists at all
            if (subscription == null)
            {
                var tenantExists = await _context.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive);
                if (!tenantExists)
                {
                    return (false, "Tenant no encontrado o inactivo");
                }

                // Tenant exists but no subscription - use Free plan limits
                _logger.LogWarning("Tenant {TenantId} no tiene suscripción, permitiendo creación con límites Free", tenantId);
                var freeLimits = PlanFeatures.GetLimits(SubscriptionPlan.Free);

                var employeeCount = await _context.Empleados
                    .CountAsync(e => e.TenantId == tenantId && e.EstaActivo);

                if (employeeCount >= freeLimits.MaxEmployees)
                {
                    return (false,
                        $"Has alcanzado el límite de {freeLimits.MaxEmployees} empleados. " +
                        "Crea una suscripción para aumentar tu límite.");
                }

                return (true, null);
            }

            // 2. Check subscription status
            if (subscription.Status == SubscriptionStatus.PastDue)
            {
                return (false, "Tu suscripción tiene un pago pendiente. Por favor actualiza tu método de pago para continuar creando empleados.");
            }

            if (subscription.Status == SubscriptionStatus.Canceled)
            {
                return (false, "Tu suscripción ha sido cancelada. Reactiva tu suscripción para continuar.");
            }

            // 3. Get plan limits
            var limits = PlanFeatures.GetLimits(subscription.Plan);

            // 4. Count current active employees
            var currentCount = await _context.Empleados
                .CountAsync(e => e.TenantId == tenantId && e.EstaActivo);

            // 5. Check limit
            if (currentCount >= limits.MaxEmployees)
            {
                var planName = subscription.Plan.ToString();
                var nextPlan = GetNextPlan(subscription.Plan);

                if (nextPlan.HasValue)
                {
                    var nextLimits = PlanFeatures.GetLimits(nextPlan.Value);
                    return (false,
                        $"Has alcanzado el límite de {limits.MaxEmployees} empleados activos en tu plan {planName}. " +
                        $"Actualiza a {nextPlan.Value} (hasta {nextLimits.MaxEmployees} empleados) para continuar.");
                }
                else
                {
                    return (false,
                        $"Has alcanzado el límite de {limits.MaxEmployees} empleados activos. " +
                        "Contacta con soporte para aumentar tu límite.");
                }
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando límite de empleados para Tenant {TenantId}", tenantId);
            return (false, "Error verificando límites. Por favor intenta de nuevo.");
        }
    }

    /// <summary>
    /// Checks if tenant can invite a new user
    /// </summary>
    public async Task<(bool allowed, string? reason)> CanInviteUserAsync(int tenantId)
    {
        try
        {
            // 1. Get subscription
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            // If no subscription exists, check if tenant exists at all
            if (subscription == null)
            {
                var tenantExists = await _context.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive);
                if (!tenantExists)
                {
                    return (false, "Tenant no encontrado o inactivo");
                }

                // Tenant exists but no subscription - use Free plan limits
                _logger.LogWarning("Tenant {TenantId} no tiene suscripción, permitiendo invitación con límites Free", tenantId);
                var freeLimits = PlanFeatures.GetLimits(SubscriptionPlan.Free);

                var activeUsersCount = await _context.TenantUsers
                    .CountAsync(tu => tu.TenantId == tenantId && tu.IsActive);

                var pendingInvitesCount = await _context.TenantInvitations
                    .CountAsync(i => i.TenantId == tenantId && i.IsActive && !i.AcceptedAt.HasValue && !i.IsRevoked && i.ExpiresAt > DateTime.UtcNow);

                var userCount = activeUsersCount + pendingInvitesCount;

                if (userCount >= freeLimits.MaxUsers)
                {
                    return (false,
                        $"Has alcanzado el límite de {freeLimits.MaxUsers} usuarios. " +
                        "Crea una suscripción para aumentar tu límite.");
                }

                return (true, null);
            }

            // 2. Check subscription status
            if (subscription.Status == SubscriptionStatus.PastDue)
            {
                return (false, "Tu suscripción tiene un pago pendiente. Por favor actualiza tu método de pago.");
            }

            if (subscription.Status == SubscriptionStatus.Canceled)
            {
                return (false, "Tu suscripción ha sido cancelada. Reactiva tu suscripción para continuar.");
            }

            // 3. Get plan limits
            var limits = PlanFeatures.GetLimits(subscription.Plan);

            // 4. Count current active users + pending invitations
            var activeUsers = await _context.TenantUsers
                .CountAsync(tu => tu.TenantId == tenantId && tu.IsActive);

            var pendingInvitations = await _context.TenantInvitations
                .CountAsync(i => i.TenantId == tenantId && i.IsActive && !i.AcceptedAt.HasValue && !i.IsRevoked && i.ExpiresAt > DateTime.UtcNow);

            var currentCount = activeUsers + pendingInvitations;

            // 5. Check limit
            if (currentCount >= limits.MaxUsers)
            {
                var planName = subscription.Plan.ToString();
                var nextPlan = GetNextPlan(subscription.Plan);

                if (nextPlan.HasValue)
                {
                    var nextLimits = PlanFeatures.GetLimits(nextPlan.Value);
                    return (false,
                        $"Has alcanzado el límite de {limits.MaxUsers} usuarios en tu plan {planName}. " +
                        $"Actualiza a {nextPlan.Value} (hasta {nextLimits.MaxUsers} usuarios) para continuar.");
                }
                else
                {
                    return (false,
                        $"Has alcanzado el límite de {limits.MaxUsers} usuarios. " +
                        "Contacta con soporte para aumentar tu límite.");
                }
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando límite de usuarios para Tenant {TenantId}", tenantId);
            return (false, "Error verificando límites. Por favor intenta de nuevo.");
        }
    }

    /// <summary>
    /// Checks if tenant can export reports (Excel/PDF)
    /// </summary>
    public async Task<bool> CanExportReportsAsync(int tenantId)
    {
        try
        {
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (subscription == null)
                return false;

            // During trial, allow exports
            if (subscription.Status == SubscriptionStatus.Trialing)
                return true;

            // For active subscriptions, check plan features
            if (subscription.Status != SubscriptionStatus.Active)
                return false;

            var limits = PlanFeatures.GetLimits(subscription.Plan);
            return limits.CanExportExcel || limits.CanExportPdf;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando permiso de exportación para Tenant {TenantId}", tenantId);
            return false;
        }
    }

    /// <summary>
    /// Checks if tenant can use API
    /// </summary>
    public async Task<bool> CanUseApiAsync(int tenantId)
    {
        try
        {
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (subscription == null)
                return false;

            // During trial, allow API
            if (subscription.Status == SubscriptionStatus.Trialing)
                return true;

            // For active subscriptions, check plan features
            if (subscription.Status != SubscriptionStatus.Active)
                return false;

            var limits = PlanFeatures.GetLimits(subscription.Plan);
            return limits.CanUseApi;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando permiso de API para Tenant {TenantId}", tenantId);
            return false;
        }
    }

    // ===========================
    // HELPER METHODS
    // ===========================

    /// <summary>
    /// Gets the next recommended plan for upgrade
    /// </summary>
    private SubscriptionPlan? GetNextPlan(SubscriptionPlan currentPlan)
    {
        return currentPlan switch
        {
            SubscriptionPlan.Free => SubscriptionPlan.Starter,
            SubscriptionPlan.Starter => SubscriptionPlan.Professional,
            SubscriptionPlan.Professional => SubscriptionPlan.Enterprise,
            SubscriptionPlan.Enterprise => null, // Already at max
            _ => null
        };
    }
}
