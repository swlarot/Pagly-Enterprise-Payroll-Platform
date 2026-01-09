using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs.Billing;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Models;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Billing Controller - User-facing subscription management
/// All endpoints require JWT authentication and respect tenant isolation
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IStripeBillingService _stripeBillingService;
    private readonly IPlanLimitService _planLimitService;
    private readonly ILogger<BillingController> _logger;

    public BillingController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        IStripeBillingService stripeBillingService,
        IPlanLimitService planLimitService,
        ILogger<BillingController> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _stripeBillingService = stripeBillingService;
        _planLimitService = planLimitService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/billing/subscription
    /// Gets current subscription status and usage
    /// </summary>
    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription()
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Get subscription
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (subscription == null)
            {
                return NotFound(new { error = "Suscripción no encontrada" });
            }

            // Get current usage
            var employeeCount = await _context.Empleados
                .CountAsync(e => e.TenantId == tenantId && e.EstaActivo);

            var userCount = await _context.TenantUsers
                .CountAsync(tu => tu.TenantId == tenantId && tu.IsActive);

            // Get plan limits
            var limits = PlanFeatures.GetLimits(subscription.Plan);

            var status = new SubscriptionStatusDto
            {
                Plan = subscription.Plan,
                PlanName = subscription.Plan.ToString(),
                Status = subscription.Status,
                StatusName = GetStatusDisplayName(subscription.Status),
                TrialEndsAt = subscription.TrialEndsAt,
                NextBillingDate = subscription.EndDate,
                MonthlyPrice = subscription.MonthlyPrice,

                MaxEmployees = limits.MaxEmployees,
                MaxUsers = limits.MaxUsers,
                CurrentEmployees = employeeCount,
                CurrentUsers = userCount,

                CanExportExcel = limits.CanExportExcel,
                CanExportPdf = limits.CanExportPdf,
                CanUseApi = limits.CanUseApi,
                HasAuditLog = limits.HasAuditLog,
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo suscripción para Tenant {TenantId}", _tenantContext.TenantId);
            return StatusCode(500, new { error = "Error obteniendo suscripción" });
        }
    }

    /// <summary>
    /// POST /api/billing/checkout
    /// Creates a Stripe Checkout Session for plan upgrade/downgrade
    /// Only Owner and Admin can access
    /// </summary>
    [HttpPost("checkout")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutRequest request)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            var userEmail = User.FindFirst("email")?.Value ?? "";

            if (string.IsNullOrEmpty(userEmail))
            {
                return BadRequest(new { error = "Email de usuario no encontrado" });
            }

            // Validate plan
            if (!Enum.IsDefined(typeof(SubscriptionPlan), request.Plan))
            {
                return BadRequest(new { error = "Plan inválido" });
            }

            if (request.Plan == SubscriptionPlan.Free)
            {
                return BadRequest(new { error = "No se puede crear checkout para plan Free. Cancela tu suscripción en su lugar." });
            }

            // Create checkout session
            var session = await _stripeBillingService.CreateCheckoutSessionAsync(
                tenantId,
                request.Plan,
                userEmail);

            _logger.LogInformation(
                "Checkout session creado para Tenant {TenantId}: {SessionId}",
                tenantId, session.SessionId);

            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando checkout para Tenant {TenantId}", _tenantContext.TenantId);
            return StatusCode(500, new { error = "Error creando checkout. Por favor intenta de nuevo." });
        }
    }

    /// <summary>
    /// POST /api/billing/portal
    /// Creates a Stripe Customer Portal session for managing subscription
    /// Only Owner and Admin can access
    /// </summary>
    [HttpPost("portal")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> CreatePortal([FromBody] CreatePortalRequest request)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Create customer portal session
            var portalUrl = await _stripeBillingService.CreateCustomerPortalSessionAsync(
                tenantId,
                request.ReturnUrl ?? $"{Request.Scheme}://{Request.Host}/dashboard");

            _logger.LogInformation(
                "Customer Portal creado para Tenant {TenantId}",
                tenantId);

            return Ok(new { url = portalUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando portal para Tenant {TenantId}", _tenantContext.TenantId);
            return StatusCode(500, new { error = "Error creando portal. Por favor intenta de nuevo." });
        }
    }

    /// <summary>
    /// POST /api/billing/cancel
    /// Cancels subscription at period end
    /// Only Owner can access
    /// </summary>
    [HttpPost("cancel")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> CancelSubscription()
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            await _stripeBillingService.CancelSubscriptionAtPeriodEndAsync(tenantId);

            _logger.LogInformation(
                "Suscripción cancelada al final del periodo para Tenant {TenantId}",
                tenantId);

            return Ok(new { message = "Suscripción cancelada al final del periodo actual" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelando suscripción para Tenant {TenantId}", _tenantContext.TenantId);
            return StatusCode(500, new { error = "Error cancelando suscripción. Por favor intenta de nuevo." });
        }
    }

    /// <summary>
    /// GET /api/billing/limits
    /// Gets current plan limits and usage
    /// </summary>
    [HttpGet("limits")]
    public async Task<IActionResult> GetLimits()
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            var limits = await _planLimitService.GetLimitsForTenantAsync(tenantId);

            // Get current usage
            var employeeCount = await _context.Empleados
                .CountAsync(e => e.TenantId == tenantId && e.EstaActivo);

            var userCount = await _context.TenantUsers
                .CountAsync(tu => tu.TenantId == tenantId && tu.IsActive);

            return Ok(new
            {
                limits = new
                {
                    maxEmployees = limits.MaxEmployees,
                    maxUsers = limits.MaxUsers,
                    maxCompanies = limits.MaxCompanies,
                    canExportExcel = limits.CanExportExcel,
                    canExportPdf = limits.CanExportPdf,
                    canUseApi = limits.CanUseApi,
                    hasEmailNotifications = limits.HasEmailNotifications,
                    hasAuditLog = limits.HasAuditLog,
                    retentionDays = limits.RetentionDays,
                    pricePerMonth = limits.PricePerMonth,
                },
                usage = new
                {
                    currentEmployees = employeeCount,
                    currentUsers = userCount,
                    employeePercentage = limits.MaxEmployees > 0 ? (employeeCount * 100.0 / limits.MaxEmployees) : 0,
                    userPercentage = limits.MaxUsers > 0 ? (userCount * 100.0 / limits.MaxUsers) : 0,
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo límites para Tenant {TenantId}", _tenantContext.TenantId);
            return StatusCode(500, new { error = "Error obteniendo límites" });
        }
    }

    // ===========================
    // HELPER METHODS
    // ===========================

    private string GetStatusDisplayName(SubscriptionStatus status)
    {
        return status switch
        {
            SubscriptionStatus.Active => "Activa",
            SubscriptionStatus.Trialing => "Prueba",
            SubscriptionStatus.PastDue => "Pago Pendiente",
            SubscriptionStatus.Canceled => "Cancelada",
            SubscriptionStatus.CanceledAtPeriodEnd => "Cancelando",
            _ => status.ToString()
        };
    }
}

// ===========================
// REQUEST MODELS
// ===========================

public class CreateCheckoutRequest
{
    public SubscriptionPlan Plan { get; set; }
}

public class CreatePortalRequest
{
    public string? ReturnUrl { get; set; }
}
