namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Estado de la suscripción del tenant
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>
    /// En período de prueba
    /// </summary>
    Trialing = 0,

    /// <summary>
    /// Suscripción activa y pagada
    /// </summary>
    Active = 1,

    /// <summary>
    /// Pago atrasado
    /// </summary>
    PastDue = 2,

    /// <summary>
    /// Suscripción cancelada
    /// </summary>
    Canceled = 3,

    /// <summary>
    /// Cancelada pero activa hasta el final del período
    /// </summary>
    CanceledAtPeriodEnd = 4,

    /// <summary>
    /// Suspendida por falta de pago
    /// </summary>
    Suspended = 5
}
