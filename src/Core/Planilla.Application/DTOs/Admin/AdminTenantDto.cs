using Vorluno.Planilla.Application.DTOs.Auth;

namespace Vorluno.Planilla.Application.DTOs.Admin;

/// <summary>
/// DTO detallado de tenant para el Admin Panel (incluye información extendida)
/// </summary>
public class AdminTenantDto
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
    /// Información del propietario (Owner)
    /// </summary>
    public OwnerInfoDto? Owner { get; set; }

    /// <summary>
    /// Estadísticas de uso
    /// </summary>
    public AdminTenantUsageDto Usage { get; set; } = new();
}

/// <summary>
/// Información del propietario del tenant
/// </summary>
public class OwnerInfoDto
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// Estadísticas de uso del tenant para el Admin Panel
/// </summary>
public class AdminTenantUsageDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalEmployees { get; set; }
    public int ActiveEmployees { get; set; }
    public int TotalPayrolls { get; set; }
    public int PendingInvitations { get; set; }

    /// <summary>
    /// Límites del plan
    /// </summary>
    public int MaxUsers { get; set; }
    public int MaxEmployees { get; set; }

    /// <summary>
    /// Porcentaje de uso
    /// </summary>
    public decimal UserUsagePercentage => MaxUsers > 0 ? (decimal)ActiveUsers / MaxUsers * 100 : 0;
    public decimal EmployeeUsagePercentage => MaxEmployees > 0 ? (decimal)ActiveEmployees / MaxEmployees * 100 : 0;
}
