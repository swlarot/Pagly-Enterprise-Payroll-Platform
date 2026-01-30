using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs.Subscription;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Models;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Controller para gestión de suscripciones y billing
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SubscriptionController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        ILogger<SubscriptionController> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/subscription/usage - Obtiene el uso actual de la suscripción
    /// Incluye: empleados, usuarios, features, límites y warnings
    /// </summary>
    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage()
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // 1. Obtener subscription con tenant
            var subscription = await _context.Subscriptions
                .Include(s => s.Tenant)
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (subscription == null)
            {
                return NotFound(new { message = "Suscripción no encontrada" });
            }

            // 2. Contar empleados activos
            var employeeCount = await _context.Empleados
                .CountAsync(e => e.TenantId == tenantId && e.EstaActivo);

            // 3. Contar usuarios activos + invitaciones pendientes
            var activeUsersCount = await _context.TenantUsers
                .CountAsync(tu => tu.TenantId == tenantId && tu.IsActive);

            var pendingInvitesCount = await _context.TenantInvitations
                .CountAsync(i => i.TenantId == tenantId && i.IsActive &&
                                !i.AcceptedAt.HasValue && !i.IsRevoked &&
                                i.ExpiresAt > DateTime.UtcNow);

            var userCount = activeUsersCount + pendingInvitesCount;

            // 4. Obtener límites del plan
            var limits = PlanFeatures.GetLimits(subscription.Plan);

            // 5. Calcular porcentajes
            var employeePercentage = limits.MaxEmployees > 0
                ? Math.Round((decimal)employeeCount / limits.MaxEmployees * 100, 2)
                : 0;

            var userPercentage = limits.MaxUsers > 0
                ? Math.Round((decimal)userCount / limits.MaxUsers * 100, 2)
                : 0;

            // 6. Generar warnings
            var warnings = new List<string>();

            // Warning: Trial ending soon
            if (subscription.TrialEndsAt.HasValue)
            {
                var daysUntilTrialEnd = (subscription.TrialEndsAt.Value - DateTime.UtcNow).Days;
                if (daysUntilTrialEnd >= 0 && daysUntilTrialEnd <= 3)
                {
                    warnings.Add($"Tu trial termina en {daysUntilTrialEnd} día(s). Actualiza tu suscripción para continuar.");
                }
            }

            // Warning: Past due
            if (subscription.Status == Domain.Enums.SubscriptionStatus.PastDue)
            {
                warnings.Add("Tu suscripción tiene un pago pendiente. Actualiza tu método de pago para continuar.");
            }

            // Warning: Canceled
            if (subscription.Status == Domain.Enums.SubscriptionStatus.Canceled)
            {
                warnings.Add("Tu suscripción ha sido cancelada. Reactiva tu suscripción para acceder a todas las funciones.");
            }

            // Warning: Employee limit reached
            if (employeeCount >= limits.MaxEmployees)
            {
                warnings.Add($"Has alcanzado el límite de {limits.MaxEmployees} empleados. Actualiza tu plan para agregar más.");
            }
            else if (employeePercentage >= 80)
            {
                warnings.Add($"Has usado {employeePercentage}% de tus empleados. Considera actualizar tu plan pronto.");
            }

            // Warning: User limit reached
            if (userCount >= limits.MaxUsers)
            {
                warnings.Add($"Has alcanzado el límite de {limits.MaxUsers} usuarios. Actualiza tu plan para invitar más.");
            }

            // 7. Construir respuesta
            var usage = new SubscriptionUsageDto
            {
                Plan = subscription.Plan,
                PlanName = subscription.Plan.ToString(),
                Status = subscription.Status,
                StatusName = subscription.Status.ToString(),
                TrialEndsAt = subscription.TrialEndsAt,

                // Employee usage
                EmployeeCount = employeeCount,
                EmployeeLimit = limits.MaxEmployees,
                EmployeePercentage = employeePercentage,

                // User usage
                UserCount = userCount,
                UserLimit = limits.MaxUsers,
                UserPercentage = userPercentage,

                // Features
                CanExportExcel = limits.CanExportExcel,
                CanExportPdf = limits.CanExportPdf,
                CanUseApi = limits.CanUseApi,
                HasEmailNotifications = limits.HasEmailNotifications,
                HasAuditLog = limits.HasAuditLog,

                // Billing
                MonthlyPrice = subscription.MonthlyPrice,
                UpgradeUrl = subscription.Plan != Domain.Enums.SubscriptionPlan.Enterprise
                    ? "/subscription/upgrade"
                    : null,
                BillingUrl = !string.IsNullOrEmpty(subscription.StripeCustomerId)
                    ? "/subscription/billing"
                    : null,

                // Warnings
                WarningMessages = warnings
            };

            _logger.LogInformation("Usage retrieved for Tenant {TenantId}: {EmployeeCount}/{EmployeeLimit} employees, {UserCount}/{UserLimit} users",
                tenantId, employeeCount, limits.MaxEmployees, userCount, limits.MaxUsers);

            return Ok(usage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription usage for tenant {TenantId}", _tenantContext.TenantId);
            return StatusCode(500, new { message = "Error al obtener uso de suscripción" });
        }
    }
}
