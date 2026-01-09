using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa la suscripción de un tenant al servicio SaaS
/// </summary>
public class Subscription : BaseEntity
{
    /// <summary>
    /// ID del tenant al que pertenece esta suscripción
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Plan de suscripción activo
    /// </summary>
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;

    /// <summary>
    /// Estado de la suscripción
    /// </summary>
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;

    /// <summary>
    /// Fecha de inicio de la suscripción
    /// </summary>
    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha de finalización de la suscripción (null si es indefinida)
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Fecha de finalización del período de prueba
    /// </summary>
    public DateTime? TrialEndsAt { get; set; }

    /// <summary>
    /// ID del cliente en Stripe
    /// </summary>
    [StringLength(100)]
    public string? StripeCustomerId { get; set; }

    /// <summary>
    /// ID de la suscripción en Stripe
    /// </summary>
    [StringLength(100)]
    public string? StripeSubscriptionId { get; set; }

    /// <summary>
    /// Precio mensual actual (puede variar de los precios por defecto)
    /// </summary>
    [Column(TypeName = "decimal(10, 2)")]
    public decimal MonthlyPrice { get; set; }

    /// <summary>
    /// Límite personalizado de empleados (0 = usar límite del plan)
    /// </summary>
    public int CustomMaxEmployees { get; set; } = 0;

    /// <summary>
    /// Límite personalizado de usuarios (0 = usar límite del plan)
    /// </summary>
    public int CustomMaxUsers { get; set; } = 0;

    /// <summary>
    /// Fecha del próximo pago
    /// </summary>
    public DateTime? NextBillingDate { get; set; }

    /// <summary>
    /// Fecha de cancelación (si aplica)
    /// </summary>
    public DateTime? CanceledAt { get; set; }

    /// <summary>
    /// Razón de cancelación
    /// </summary>
    [StringLength(500)]
    public string? CancellationReason { get; set; }

    // Navegación
    public virtual Tenant Tenant { get; set; } = null!;

    /// <summary>
    /// Obtiene el límite efectivo de empleados (personalizado o del plan)
    /// </summary>
    public int GetEffectiveMaxEmployees()
    {
        if (CustomMaxEmployees > 0)
            return CustomMaxEmployees;

        return Models.PlanFeatures.GetLimits(Plan).MaxEmployees;
    }

    /// <summary>
    /// Obtiene el límite efectivo de usuarios (personalizado o del plan)
    /// </summary>
    public int GetEffectiveMaxUsers()
    {
        if (CustomMaxUsers > 0)
            return CustomMaxUsers;

        return Models.PlanFeatures.GetLimits(Plan).MaxUsers;
    }

    /// <summary>
    /// Verifica si la suscripción está activa o en trial
    /// </summary>
    public bool IsActiveOrTrialing()
    {
        return Status == SubscriptionStatus.Active || Status == SubscriptionStatus.Trialing;
    }

    /// <summary>
    /// Verifica si el trial ha expirado
    /// </summary>
    public bool IsTrialExpired()
    {
        return Status == SubscriptionStatus.Trialing &&
               TrialEndsAt.HasValue &&
               TrialEndsAt.Value < DateTime.UtcNow;
    }
}
