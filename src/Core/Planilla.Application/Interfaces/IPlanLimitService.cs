using Vorluno.Planilla.Domain.Models;

namespace Vorluno.Planilla.Application.Interfaces;

public interface IPlanLimitService
{
    /// <summary>
    /// Gets plan limits for a tenant based on their subscription
    /// </summary>
    Task<PlanLimits> GetLimitsForTenantAsync(int tenantId);

    /// <summary>
    /// Checks if tenant can create a new employee
    /// </summary>
    Task<(bool allowed, string? reason)> CanCreateEmployeeAsync(int tenantId);

    /// <summary>
    /// Checks if tenant can invite a new user
    /// </summary>
    Task<(bool allowed, string? reason)> CanInviteUserAsync(int tenantId);

    /// <summary>
    /// Checks if tenant can export reports
    /// </summary>
    Task<bool> CanExportReportsAsync(int tenantId);

    /// <summary>
    /// Checks if tenant can use API
    /// </summary>
    Task<bool> CanUseApiAsync(int tenantId);
}
