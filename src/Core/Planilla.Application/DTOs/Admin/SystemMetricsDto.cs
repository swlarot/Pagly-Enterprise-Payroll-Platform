namespace Vorluno.Planilla.Application.DTOs.Admin;

/// <summary>
/// Métricas generales del sistema para el Admin Panel
/// </summary>
public class SystemMetricsDto
{
    /// <summary>
    /// Total de tenants en el sistema
    /// </summary>
    public int TotalTenants { get; set; }

    /// <summary>
    /// Tenants activos
    /// </summary>
    public int ActiveTenants { get; set; }

    /// <summary>
    /// Total de usuarios registrados en todos los tenants
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    /// Total de empleados registrados en todos los tenants
    /// </summary>
    public int TotalEmployees { get; set; }

    /// <summary>
    /// Distribución por plan de suscripción
    /// </summary>
    public PlanDistributionDto PlanDistribution { get; set; } = new();

    /// <summary>
    /// Crecimiento reciente del sistema
    /// </summary>
    public RecentGrowthDto RecentGrowth { get; set; } = new();
}

/// <summary>
/// Distribución de tenants por plan
/// </summary>
public class PlanDistributionDto
{
    public int Free { get; set; }
    public int Starter { get; set; }
    public int Professional { get; set; }
    public int Enterprise { get; set; }
}

/// <summary>
/// Métricas de crecimiento reciente
/// </summary>
public class RecentGrowthDto
{
    public GrowthPeriodDto Last7Days { get; set; } = new();
    public GrowthPeriodDto Last30Days { get; set; } = new();
}

/// <summary>
/// Métricas de un período de crecimiento
/// </summary>
public class GrowthPeriodDto
{
    public int NewTenants { get; set; }
    public int NewUsers { get; set; }
}
