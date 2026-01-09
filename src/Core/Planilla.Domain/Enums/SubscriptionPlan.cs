namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Planes de suscripción disponibles en Vorluno Planilla SaaS
/// </summary>
public enum SubscriptionPlan
{
    /// <summary>
    /// Plan gratuito: 5 empleados, 1 usuario, reportes básicos
    /// </summary>
    Free = 0,

    /// <summary>
    /// Plan Starter: 25 empleados, 3 usuarios, exportación Excel
    /// </summary>
    Starter = 1,

    /// <summary>
    /// Plan Professional: 100 empleados, 10 usuarios, PDF + Excel + API
    /// </summary>
    Professional = 2,

    /// <summary>
    /// Plan Enterprise: Empleados y usuarios ilimitados, soporte prioritario
    /// </summary>
    Enterprise = 3
}
