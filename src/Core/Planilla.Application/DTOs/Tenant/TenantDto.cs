using Vorluno.Planilla.Application.DTOs.Auth;

namespace Vorluno.Planilla.Application.DTOs.Tenant;

/// <summary>
/// DTO completo con información del tenant incluyendo suscripción y métricas de uso
/// </summary>
public class TenantDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string? RUC { get; set; }
    public string? DV { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }

    /// <summary>
    /// Información de la suscripción activa
    /// </summary>
    public SubscriptionInfoDto? Subscription { get; set; }

    /// <summary>
    /// Métricas de uso del tenant
    /// </summary>
    public TenantUsageDto? Usage { get; set; }
}
