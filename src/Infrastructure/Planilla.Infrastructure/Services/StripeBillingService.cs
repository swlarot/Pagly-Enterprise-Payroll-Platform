using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Vorluno.Planilla.Application.DTOs.Billing;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Configuration;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Stripe Billing Service - Manages subscriptions, checkout, and customer portal
/// </summary>
public class StripeBillingService : IStripeBillingService
{
    private readonly ApplicationDbContext _context;
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<StripeBillingService> _logger;

    public StripeBillingService(
        ApplicationDbContext context,
        IOptions<StripeOptions> stripeOptions,
        ILogger<StripeBillingService> logger)
    {
        _context = context;
        _stripeOptions = stripeOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates a Stripe Checkout Session for upgrading/downgrading plan
    /// </summary>
    public async Task<CheckoutSessionDto> CreateCheckoutSessionAsync(
        int tenantId,
        SubscriptionPlan targetPlan,
        string userEmail)
    {
        try
        {
            // 1. Get tenant with subscription
            var tenant = await _context.Tenants
                .Include(t => t.Subscription)
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
                throw new InvalidOperationException($"Tenant {tenantId} no encontrado");

            if (tenant.Subscription == null)
                throw new InvalidOperationException($"Tenant {tenantId} no tiene suscripción");

            // 2. Validate target plan
            if (targetPlan == SubscriptionPlan.Free)
                throw new InvalidOperationException("No se puede crear checkout para plan Free");

            // 3. Get Stripe Price ID for target plan
            var priceId = GetPriceIdForPlan(targetPlan);

            // 4. Create or get Stripe customer
            var customerId = await GetOrCreateStripeCustomerAsync(tenant, userEmail);

            // 5. Create Checkout Session
            var options = new Stripe.Checkout.SessionCreateOptions
            {
                Customer = customerId,
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
                {
                    new Stripe.Checkout.SessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = 1,
                    },
                },
                Mode = "subscription",
                SuccessUrl = _stripeOptions.SuccessUrl,
                CancelUrl = _stripeOptions.CancelUrl,
                Metadata = new Dictionary<string, string>
                {
                    { "tenant_id", tenantId.ToString() },
                    { "plan", targetPlan.ToString() },
                },
                SubscriptionData = new Stripe.Checkout.SessionSubscriptionDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        { "tenant_id", tenantId.ToString() },
                        { "plan", targetPlan.ToString() },
                    },
                },
            };

            var service = new Stripe.Checkout.SessionService();
            var session = await service.CreateAsync(options);

            _logger.LogInformation(
                "Stripe Checkout Session creado: {SessionId} para Tenant {TenantId} (Plan: {Plan})",
                session.Id, tenantId, targetPlan);

            return new CheckoutSessionDto
            {
                SessionId = session.Id,
                CheckoutUrl = session.Url
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando Stripe Checkout Session para Tenant {TenantId}", tenantId);
            throw;
        }
    }

    /// <summary>
    /// Creates a Customer Portal session for managing subscription
    /// </summary>
    public async Task<string> CreateCustomerPortalSessionAsync(int tenantId, string returnUrl)
    {
        try
        {
            // 1. Get tenant with subscription
            var tenant = await _context.Tenants
                .Include(t => t.Subscription)
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
                throw new InvalidOperationException($"Tenant {tenantId} no encontrado");

            if (tenant.Subscription == null || string.IsNullOrEmpty(tenant.Subscription.StripeCustomerId))
                throw new InvalidOperationException($"Tenant {tenantId} no tiene cliente de Stripe");

            // 2. Create Customer Portal session
            var options = new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = tenant.Subscription.StripeCustomerId,
                ReturnUrl = returnUrl,
            };

            var service = new Stripe.BillingPortal.SessionService();
            var session = await service.CreateAsync(options);

            _logger.LogInformation(
                "Stripe Customer Portal creado para Tenant {TenantId}: {SessionId}",
                tenantId, session.Id);

            return session.Url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando Stripe Customer Portal para Tenant {TenantId}", tenantId);
            throw;
        }
    }

    /// <summary>
    /// Cancels subscription at period end
    /// </summary>
    public async Task CancelSubscriptionAtPeriodEndAsync(int tenantId)
    {
        try
        {
            // 1. Get tenant with subscription
            var tenant = await _context.Tenants
                .Include(t => t.Subscription)
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
                throw new InvalidOperationException($"Tenant {tenantId} no encontrado");

            if (tenant.Subscription == null || string.IsNullOrEmpty(tenant.Subscription.StripeSubscriptionId))
                throw new InvalidOperationException($"Tenant {tenantId} no tiene suscripción activa");

            // 2. Cancel subscription in Stripe
            var service = new SubscriptionService();
            var options = new SubscriptionUpdateOptions
            {
                CancelAtPeriodEnd = true,
            };

            var subscription = await service.UpdateAsync(tenant.Subscription.StripeSubscriptionId, options);

            // 3. Update local subscription status
            tenant.Subscription.Status = SubscriptionStatus.CanceledAtPeriodEnd;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Suscripción cancelada al final del periodo para Tenant {TenantId}: {SubscriptionId}",
                tenantId, subscription.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelando suscripción para Tenant {TenantId}", tenantId);
            throw;
        }
    }

    /// <summary>
    /// Changes plan immediately via Stripe subscription update
    /// </summary>
    public async Task ChangePlanAsync(int tenantId, SubscriptionPlan targetPlan)
    {
        try
        {
            // 1. Get tenant with subscription
            var tenant = await _context.Tenants
                .Include(t => t.Subscription)
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
                throw new InvalidOperationException($"Tenant {tenantId} no encontrado");

            if (tenant.Subscription == null || string.IsNullOrEmpty(tenant.Subscription.StripeSubscriptionId))
                throw new InvalidOperationException($"Tenant {tenantId} no tiene suscripción activa");

            // 2. Get target price ID
            var priceId = GetPriceIdForPlan(targetPlan);

            // 3. Get current subscription from Stripe
            var subscriptionService = new SubscriptionService();
            var currentSubscription = await subscriptionService.GetAsync(tenant.Subscription.StripeSubscriptionId);

            if (currentSubscription.Items.Data.Count == 0)
                throw new InvalidOperationException("Suscripción sin items");

            var currentItemId = currentSubscription.Items.Data[0].Id;

            // 4. Update subscription with new price
            var options = new SubscriptionUpdateOptions
            {
                Items = new List<SubscriptionItemOptions>
                {
                    new SubscriptionItemOptions
                    {
                        Id = currentItemId,
                        Price = priceId,
                    },
                },
                ProrationBehavior = "create_prorations", // Prorate immediately
                Metadata = new Dictionary<string, string>
                {
                    { "tenant_id", tenantId.ToString() },
                    { "plan", targetPlan.ToString() },
                },
            };

            var updatedSubscription = await subscriptionService.UpdateAsync(
                tenant.Subscription.StripeSubscriptionId,
                options);

            _logger.LogInformation(
                "Plan cambiado para Tenant {TenantId}: {OldPlan} -> {NewPlan}",
                tenantId, tenant.Subscription.Plan, targetPlan);

            // Note: Webhook will update local subscription
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cambiando plan para Tenant {TenantId}", tenantId);
            throw;
        }
    }

    // ===========================
    // HELPER METHODS
    // ===========================

    /// <summary>
    /// Gets or creates a Stripe customer for the tenant
    /// </summary>
    private async Task<string> GetOrCreateStripeCustomerAsync(
        Domain.Entities.Tenant tenant,
        string userEmail)
    {
        // If customer already exists, return it
        if (!string.IsNullOrEmpty(tenant.Subscription?.StripeCustomerId))
            return tenant.Subscription.StripeCustomerId;

        // Create new customer
        var options = new CustomerCreateOptions
        {
            Email = userEmail,
            Name = tenant.Name,
            Metadata = new Dictionary<string, string>
            {
                { "tenant_id", tenant.Id.ToString() },
                { "ruc", $"{tenant.RUC}-{tenant.DV}" },
            },
        };

        var service = new CustomerService();
        var customer = await service.CreateAsync(options);

        // Save customer ID to subscription
        if (tenant.Subscription != null)
        {
            tenant.Subscription.StripeCustomerId = customer.Id;
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation(
            "Stripe Customer creado: {CustomerId} para Tenant {TenantId}",
            customer.Id, tenant.Id);

        return customer.Id;
    }

    /// <summary>
    /// Maps SubscriptionPlan to Stripe Price ID
    /// </summary>
    private string GetPriceIdForPlan(SubscriptionPlan plan)
    {
        return plan switch
        {
            SubscriptionPlan.Starter => _stripeOptions.PriceIdStarter,
            SubscriptionPlan.Professional => _stripeOptions.PriceIdProfessional,
            SubscriptionPlan.Enterprise => _stripeOptions.PriceIdEnterprise,
            SubscriptionPlan.Free => throw new InvalidOperationException("Plan Free no tiene Price ID"),
            _ => throw new ArgumentOutOfRangeException(nameof(plan), plan, "Plan no válido")
        };
    }
}
