namespace Vorluno.Planilla.Infrastructure.Configuration;

/// <summary>
/// Stripe configuration loaded from environment variables (NOT appsettings for production)
/// </summary>
public class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>
    /// Stripe Secret Key (sk_test_... or sk_live_...)
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Webhook signing secret (whsec_...)
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Stripe Price ID for Starter plan
    /// </summary>
    public string PriceIdStarter { get; set; } = string.Empty;

    /// <summary>
    /// Stripe Price ID for Professional plan
    /// </summary>
    public string PriceIdProfessional { get; set; } = string.Empty;

    /// <summary>
    /// Stripe Price ID for Enterprise plan
    /// </summary>
    public string PriceIdEnterprise { get; set; } = string.Empty;

    /// <summary>
    /// URL to redirect after successful checkout
    /// </summary>
    public string SuccessUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL to redirect after canceled checkout
    /// </summary>
    public string CancelUrl { get; set; } = string.Empty;

    /// <summary>
    /// Validates that all required configuration is present
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SecretKey))
            throw new InvalidOperationException("Stripe SecretKey is required");

        if (string.IsNullOrWhiteSpace(WebhookSecret))
            throw new InvalidOperationException("Stripe WebhookSecret is required");

        if (string.IsNullOrWhiteSpace(SuccessUrl))
            throw new InvalidOperationException("Stripe SuccessUrl is required");

        if (string.IsNullOrWhiteSpace(CancelUrl))
            throw new InvalidOperationException("Stripe CancelUrl is required");
    }
}
