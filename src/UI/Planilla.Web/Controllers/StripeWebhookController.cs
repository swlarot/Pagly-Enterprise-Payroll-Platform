using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Models;
using Vorluno.Planilla.Infrastructure.Configuration;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Stripe Webhook Controller - Handles Stripe events
/// CRITICAL: NO JWT required, signature verification MANDATORY
/// </summary>
[ApiController]
[Route("api/stripe")]
public class StripeWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        ApplicationDbContext context,
        IOptions<StripeOptions> stripeOptions,
        ILogger<StripeWebhookController> logger)
    {
        _context = context;
        _stripeOptions = stripeOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Stripe Webhook Endpoint
    /// URL: https://yourapp.com/api/stripe/webhook
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleWebhook()
    {
        try
        {
            // 1. Read request body
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            // 2. Verify Stripe signature
            Event stripeEvent;
            try
            {
                var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    signatureHeader,
                    _stripeOptions.WebhookSecret
                );
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Error verificando firma de Stripe webhook");
                return BadRequest(new { error = "Firma inválida" });
            }

            _logger.LogInformation(
                "Stripe webhook recibido: {EventType} - {EventId}",
                stripeEvent.Type, stripeEvent.Id);

            // 3. Check idempotency (prevent duplicate processing)
            var existingEvent = await _context.StripeWebhookEvents
                .FirstOrDefaultAsync(e => e.StripeEventId == stripeEvent.Id);

            if (existingEvent != null)
            {
                _logger.LogInformation(
                    "Evento {EventId} ya fue procesado (Status: {Status})",
                    stripeEvent.Id, existingEvent.Status);
                return Ok(new { message = "Evento ya procesado" });
            }

            // 4. Create webhook event record
            var webhookEvent = new StripeWebhookEvent
            {
                StripeEventId = stripeEvent.Id,
                Type = stripeEvent.Type,
                EventCreatedAt = stripeEvent.Created,
                Status = "Pending",
                RawPayload = json
            };

            _context.StripeWebhookEvents.Add(webhookEvent);
            await _context.SaveChangesAsync();

            // 5. Handle event type
            try
            {
                await HandleEventAsync(stripeEvent, webhookEvent);

                // Mark as processed
                webhookEvent.Status = "Processed";
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Evento {EventId} procesado exitosamente: {EventType}",
                    stripeEvent.Id, stripeEvent.Type);
            }
            catch (Exception ex)
            {
                // Mark as failed
                webhookEvent.Status = "Failed";
                webhookEvent.ErrorMessage = ex.Message;
                await _context.SaveChangesAsync();

                _logger.LogError(ex,
                    "Error procesando evento {EventId} ({EventType})",
                    stripeEvent.Id, stripeEvent.Type);

                // Still return 200 to Stripe to avoid retries
            }

            return Ok(new { received = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error general en webhook de Stripe");
            return StatusCode(500, new { error = "Error interno" });
        }
    }

    // ===========================
    // EVENT HANDLERS
    // ===========================

    private async Task HandleEventAsync(Event stripeEvent, StripeWebhookEvent webhookEvent)
    {
        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutSessionCompleted(stripeEvent, webhookEvent);
                break;

            case "customer.subscription.created":
                await HandleSubscriptionCreated(stripeEvent, webhookEvent);
                break;

            case "customer.subscription.updated":
                await HandleSubscriptionUpdated(stripeEvent, webhookEvent);
                break;

            case "customer.subscription.deleted":
                await HandleSubscriptionDeleted(stripeEvent, webhookEvent);
                break;

            case "invoice.payment_succeeded":
                await HandleInvoicePaymentSucceeded(stripeEvent, webhookEvent);
                break;

            case "invoice.payment_failed":
                await HandleInvoicePaymentFailed(stripeEvent, webhookEvent);
                break;

            case "customer.subscription.trial_will_end":
                await HandleTrialWillEnd(stripeEvent, webhookEvent);
                break;

            default:
                _logger.LogInformation(
                    "Evento no manejado: {EventType}",
                    stripeEvent.Type);
                break;
        }
    }

    /// <summary>
    /// checkout.session.completed - User completed checkout
    /// </summary>
    private async Task HandleCheckoutSessionCompleted(Event stripeEvent, StripeWebhookEvent webhookEvent)
    {
        var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
        if (session == null) return;

        // Get tenant ID from metadata
        if (!session.Metadata.TryGetValue("tenant_id", out var tenantIdStr) ||
            !int.TryParse(tenantIdStr, out var tenantId))
        {
            _logger.LogWarning("Checkout session sin tenant_id: {SessionId}", session.Id);
            return;
        }

        webhookEvent.TenantId = tenantId;

        // Get tenant with subscription
        var tenant = await _context.Tenants
            .Include(t => t.Subscription)
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant?.Subscription == null)
        {
            _logger.LogError("Tenant {TenantId} o Subscription no encontrados", tenantId);
            return;
        }

        // Update subscription with Stripe IDs
        tenant.Subscription.StripeCustomerId = session.CustomerId;
        tenant.Subscription.StripeSubscriptionId = session.SubscriptionId;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Checkout completado para Tenant {TenantId}: {SubscriptionId}",
            tenantId, session.SubscriptionId);
    }

    /// <summary>
    /// customer.subscription.created - New subscription created
    /// </summary>
    private async Task HandleSubscriptionCreated(Event stripeEvent, StripeWebhookEvent webhookEvent)
    {
        var subscription = stripeEvent.Data.Object as Stripe.Subscription;
        if (subscription == null) return;

        // Get tenant ID from metadata
        if (!subscription.Metadata.TryGetValue("tenant_id", out var tenantIdStr) ||
            !int.TryParse(tenantIdStr, out var tenantId))
        {
            _logger.LogWarning("Subscription sin tenant_id: {SubscriptionId}", subscription.Id);
            return;
        }

        webhookEvent.TenantId = tenantId;

        // Update subscription in database
        await UpdateSubscriptionFromStripeAsync(tenantId, subscription);
    }

    /// <summary>
    /// customer.subscription.updated - Subscription changed
    /// </summary>
    private async Task HandleSubscriptionUpdated(Event stripeEvent, StripeWebhookEvent webhookEvent)
    {
        var subscription = stripeEvent.Data.Object as Stripe.Subscription;
        if (subscription == null) return;

        // Get tenant ID from metadata
        if (!subscription.Metadata.TryGetValue("tenant_id", out var tenantIdStr) ||
            !int.TryParse(tenantIdStr, out var tenantId))
        {
            // Try to find by Stripe subscription ID
            var existingSubscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription.Id);

            if (existingSubscription != null)
            {
                tenantId = existingSubscription.TenantId;
            }
            else
            {
                _logger.LogWarning("Subscription sin tenant_id: {SubscriptionId}", subscription.Id);
                return;
            }
        }

        webhookEvent.TenantId = tenantId;

        // Update subscription in database
        await UpdateSubscriptionFromStripeAsync(tenantId, subscription);
    }

    /// <summary>
    /// customer.subscription.deleted - Subscription canceled
    /// </summary>
    private async Task HandleSubscriptionDeleted(Event stripeEvent, StripeWebhookEvent webhookEvent)
    {
        var subscription = stripeEvent.Data.Object as Stripe.Subscription;
        if (subscription == null) return;

        // Find by Stripe subscription ID
        var localSubscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription.Id);

        if (localSubscription == null)
        {
            _logger.LogWarning("Subscription local no encontrada: {SubscriptionId}", subscription.Id);
            return;
        }

        webhookEvent.TenantId = localSubscription.TenantId;

        // Set to Canceled and downgrade to Free
        localSubscription.Status = SubscriptionStatus.Canceled;
        localSubscription.Plan = SubscriptionPlan.Free;
        localSubscription.MonthlyPrice = 0;
        localSubscription.CanceledAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Subscription cancelada para Tenant {TenantId}, downgrade a Free",
            localSubscription.TenantId);
    }

    /// <summary>
    /// invoice.payment_succeeded - Payment successful
    /// </summary>
    private async Task HandleInvoicePaymentSucceeded(Event stripeEvent, StripeWebhookEvent webhookEvent)
    {
        var invoice = stripeEvent.Data.Object as Stripe.Invoice;
        if (invoice == null) return;

        // Get subscription ID from invoice
        var subscriptionId = invoice.Lines?.Data?.FirstOrDefault()?.SubscriptionId;
        if (string.IsNullOrEmpty(subscriptionId)) return;

        // Find subscription by Stripe ID
        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId);

        if (subscription == null) return;

        webhookEvent.TenantId = subscription.TenantId;

        // If was PastDue, reactivate
        if (subscription.Status == SubscriptionStatus.PastDue)
        {
            subscription.Status = SubscriptionStatus.Active;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Subscription reactivada para Tenant {TenantId} tras pago exitoso",
                subscription.TenantId);
        }
    }

    /// <summary>
    /// invoice.payment_failed - Payment failed
    /// </summary>
    private async Task HandleInvoicePaymentFailed(Event stripeEvent, StripeWebhookEvent webhookEvent)
    {
        var invoice = stripeEvent.Data.Object as Stripe.Invoice;
        if (invoice == null) return;

        // Get subscription ID from invoice
        var subscriptionId = invoice.Lines?.Data?.FirstOrDefault()?.SubscriptionId;
        if (string.IsNullOrEmpty(subscriptionId)) return;

        // Find subscription by Stripe ID
        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId);

        if (subscription == null) return;

        webhookEvent.TenantId = subscription.TenantId;

        // Set to PastDue
        subscription.Status = SubscriptionStatus.PastDue;
        await _context.SaveChangesAsync();

        _logger.LogWarning(
            "Pago fallido para Tenant {TenantId}, subscription marcada como PastDue",
            subscription.TenantId);

        // TODO: Send email notification
    }

    /// <summary>
    /// customer.subscription.trial_will_end - Trial ending in 3 days
    /// </summary>
    private async Task HandleTrialWillEnd(Event stripeEvent, StripeWebhookEvent webhookEvent)
    {
        var subscription = stripeEvent.Data.Object as Stripe.Subscription;
        if (subscription == null) return;

        // Find subscription
        var localSubscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription.Id);

        if (localSubscription == null) return;

        webhookEvent.TenantId = localSubscription.TenantId;

        _logger.LogInformation(
            "Trial terminando pronto para Tenant {TenantId}",
            localSubscription.TenantId);

        // TODO: Send email reminder
    }

    // ===========================
    // HELPER METHODS
    // ===========================

    /// <summary>
    /// Updates local subscription from Stripe subscription object
    /// </summary>
    private async Task UpdateSubscriptionFromStripeAsync(int tenantId, Stripe.Subscription stripeSubscription)
    {
        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);

        if (subscription == null)
        {
            _logger.LogError("Subscription no encontrada para Tenant {TenantId}", tenantId);
            return;
        }

        // Update Stripe IDs
        subscription.StripeSubscriptionId = stripeSubscription.Id;
        subscription.StripeCustomerId = stripeSubscription.CustomerId;

        // Update status
        subscription.Status = stripeSubscription.Status switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trialing,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Canceled,
            _ => subscription.Status
        };

        // Update trial end date
        if (stripeSubscription.TrialEnd.HasValue)
        {
            subscription.TrialEndsAt = stripeSubscription.TrialEnd.Value;
        }

        // Get plan from metadata and update
        if (stripeSubscription.Metadata.TryGetValue("plan", out var planStr) &&
            Enum.TryParse<SubscriptionPlan>(planStr, out var plan))
        {
            subscription.Plan = plan;

            // Update monthly price based on plan
            var limits = PlanFeatures.GetLimits(plan);
            subscription.MonthlyPrice = limits.PricePerMonth;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Subscription actualizada para Tenant {TenantId}: Plan={Plan}, Status={Status}",
            tenantId, subscription.Plan, subscription.Status);
    }
}
