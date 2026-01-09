using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Stores processed Stripe webhook events for idempotency
/// Prevents duplicate processing of the same event
/// </summary>
public class StripeWebhookEvent : BaseEntity
{
    /// <summary>
    /// Stripe Event ID (evt_...)
    /// </summary>
    [Required]
    [StringLength(100)]
    public string StripeEventId { get; set; } = string.Empty;

    /// <summary>
    /// Event type (e.g., "checkout.session.completed")
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Tenant ID (nullable - some events may not be tenant-specific)
    /// </summary>
    public int? TenantId { get; set; }

    /// <summary>
    /// When Stripe created the event
    /// </summary>
    public DateTime EventCreatedAt { get; set; }

    /// <summary>
    /// When we processed the event
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Processing status: Pending, Processed, Failed
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Raw JSON payload (for debugging)
    /// </summary>
    public string? RawPayload { get; set; }

    // Navigation
    public virtual Tenant? Tenant { get; set; }
}
