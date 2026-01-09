using Vorluno.Planilla.Application.DTOs.Billing;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Interfaces;

public interface IStripeBillingService
{
    /// <summary>
    /// Creates a Stripe Checkout Session for upgrading/downgrading plan
    /// </summary>
    Task<CheckoutSessionDto> CreateCheckoutSessionAsync(int tenantId, SubscriptionPlan targetPlan, string userEmail);

    /// <summary>
    /// Creates a Customer Portal session for managing subscription
    /// </summary>
    Task<string> CreateCustomerPortalSessionAsync(int tenantId, string returnUrl);

    /// <summary>
    /// Cancels subscription at period end
    /// </summary>
    Task CancelSubscriptionAtPeriodEndAsync(int tenantId);

    /// <summary>
    /// Changes plan immediately via Stripe subscription update
    /// </summary>
    Task ChangePlanAsync(int tenantId, SubscriptionPlan targetPlan);
}
