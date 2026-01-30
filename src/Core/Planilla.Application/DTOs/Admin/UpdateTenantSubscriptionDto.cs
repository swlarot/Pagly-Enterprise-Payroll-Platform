using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs.Admin;

/// <summary>
/// DTO para actualizar la suscripción de un tenant desde el panel de admin
/// </summary>
public class UpdateTenantSubscriptionDto
{
    /// <summary>
    /// Nuevo plan de suscripción
    /// </summary>
    public SubscriptionPlan Plan { get; set; }

    /// <summary>
    /// Días adicionales para extender el trial (opcional)
    /// </summary>
    public int? ExtendTrialDays { get; set; }
}
